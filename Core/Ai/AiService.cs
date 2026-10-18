using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace Linkpearl.Core.Ai;

/// <summary>
/// Watches incoming chat, triggers the configured agents, and stores generated
/// suggestions for the browser and the in-game window. All API calls are async;
/// game-chat sends are marshalled back to the framework thread.
/// </summary>
public sealed class AiService : IDisposable
{
    private const int MaxSuggestions = 20;
    private const int MaxActivity = 50;
    private const int MaxReplyBytes = 420; // leaves room for the channel/tell prefix (500-byte game limit)
    private const int ContextBufferSize = 500;

    private readonly Plugin _plugin;
    private readonly ChatReceiver _receiver;
    private readonly ChatSender _sender;
    private readonly IPluginLog _log;
    private readonly AiClient _client;

    private readonly List<AiSuggestionSet> _suggestions = [];
    private readonly List<AiActivityEntry> _activity = [];
    private readonly LinkedList<ReceivedChatMessage> _contextBuffer = new();
    private readonly object _contextGate = new();
    private int _liveMessageCount;
    private bool _backfillDone;
    private readonly Dictionary<string, DateTimeOffset> _lastTrigger = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _recentTriggers = new();
    private readonly Dictionary<string, CancellationTokenSource> _requestCts = new();
    private readonly HashSet<string> _inFlight = [];
    private readonly SemaphoreSlim _concurrency = new(2);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();

    public event Action<AiSuggestionSet>? SuggestionAdded;
    public event Action<string>? SuggestionRemoved;
    public event Action<AiActivityEntry>? ActivityAdded;

    public AiService(Plugin plugin, ChatSender sender, ChatReceiver receiver, IPluginLog log)
    {
        _plugin = plugin;
        _sender = sender;
        _receiver = receiver;
        _log = log;

        _client = new AiClient(log);
        _receiver.OnMessageReceived += OnMessageReceived;
        _receiver.OnAnyMessageReceived += OnAnyMessageReceived;
    }

    /// <summary>
    /// Repairs missing identifiers and out-of-range values after loading configuration.
    /// Returns true when the configuration was changed and should be saved.
    /// </summary>
    public bool EnsureDefaults()
    {
        var config = _plugin.Config;
        var isEnglish = string.Equals(config.UiLanguage, "en", StringComparison.OrdinalIgnoreCase);
        var changed = false;

        if (config.AiAgents is null)
        {
            config.AiAgents = new List<AiAgentConfig>();
            changed = true;
        }
        if (config.AiRules is null)
        {
            config.AiRules = new List<AiTriggerRule>();
            changed = true;
        }

        if (config.AiAgents.Count == 0)
        {
            config.AiAgents.Add(new AiAgentConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = isEnglish ? "Default Assistant" : "默认助手",
                SystemPrompt = isEnglish
                    ? "You are a friendly, concise assistant in the MMORPG Final Fantasy XIV. Reply naturally based on the conversation."
                    : "你是一名友好、简洁的《最终幻想14》游戏聊天助手。请根据对话上下文自然地回复。",
                Temperature = 0.7,
                MaxTokens = 300,
            });
            changed = true;
        }

        foreach (var agent in config.AiAgents)
        {
            if (string.IsNullOrEmpty(agent.Id))
            {
                agent.Id = Guid.NewGuid().ToString("N");
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(agent.Name))
            {
                agent.Name = isEnglish ? "Assistant" : "助手";
                changed = true;
            }

            var temperature = Math.Clamp(agent.Temperature, 0.0, 2.0);
            if (Math.Abs(temperature - agent.Temperature) > 0.0001)
            {
                agent.Temperature = temperature;
                changed = true;
            }

            var maxTokens = Math.Clamp(agent.MaxTokens, 1, 4096);
            if (maxTokens != agent.MaxTokens)
            {
                agent.MaxTokens = maxTokens;
                changed = true;
            }
        }

        var defaultAgentId = config.AiAgents[0].Id;
        foreach (var rule in config.AiRules)
        {
            if (string.IsNullOrEmpty(rule.Id))
            {
                rule.Id = Guid.NewGuid().ToString("N");
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                rule.Name = isEnglish ? "Rule" : "规则";
                changed = true;
            }
            if (string.IsNullOrEmpty(rule.AgentId) || config.AiAgents.All(a => a.Id != rule.AgentId))
            {
                rule.AgentId = defaultAgentId;
                changed = true;
            }
            if (rule.Mode is not (AiReplyModes.Auto or AiReplyModes.Options or AiReplyModes.Suggestion))
            {
                rule.Mode = AiReplyModes.Auto;
                changed = true;
            }

            var channels = rule.ChannelTypes.Where(AiChannels.IsChatType).Distinct().ToList();
            if (channels.Count != rule.ChannelTypes.Count)
            {
                rule.ChannelTypes = channels;
                changed = true;
            }

            var cooldown = Math.Clamp(rule.CooldownSeconds, 0, 3600);
            if (cooldown != rule.CooldownSeconds)
            {
                rule.CooldownSeconds = cooldown;
                changed = true;
            }

            var perMinute = Math.Clamp(rule.MaxPerMinute, 1, 120);
            if (perMinute != rule.MaxPerMinute)
            {
                rule.MaxPerMinute = perMinute;
                changed = true;
            }

            var optionsCount = Math.Clamp(rule.OptionsCount, 1, 10);
            if (optionsCount != rule.OptionsCount)
            {
                rule.OptionsCount = optionsCount;
                changed = true;
            }
        }

        var contextCount = Math.Clamp(config.AiContextMessageCount, 1, 100);
        if (contextCount != config.AiContextMessageCount)
        {
            config.AiContextMessageCount = contextCount;
            changed = true;
        }

        var timeout = Math.Clamp(config.AiRequestTimeoutSeconds, 5, 120);
        if (timeout != config.AiRequestTimeoutSeconds)
        {
            config.AiRequestTimeoutSeconds = timeout;
            changed = true;
        }

        return changed;
    }

    // ── Public surface ──────────────────────────────────────────────

    public IReadOnlyList<AiSuggestionSet> GetSuggestions()
    {
        lock (_gate) return _suggestions.ToArray();
    }

    public IReadOnlyList<AiActivityEntry> GetActivity()
    {
        lock (_gate) return _activity.ToArray();
    }

    public Task<AiTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var config = _plugin.Config;
        var model = !string.IsNullOrWhiteSpace(config.AiDefaultModel)
            ? config.AiDefaultModel
            : config.AiAgents.FirstOrDefault()?.Model ?? "";

        return _client.TestAsync(
            config.AiApiBaseUrl,
            config.AiApiKey,
            model,
            config.AiRequestTimeoutSeconds,
            cancellationToken);
    }

    /// <summary>Sends a stored candidate (or an edited suggestion) into game chat.</summary>
    public bool TrySendSuggestion(string id, int? index, string? text, out string error)
    {
        AiSuggestionSet? set;
        string? candidate = null;

        lock (_gate)
        {
            set = _suggestions.FirstOrDefault(s => s.Id == id);
            if (set == null)
            {
                error = "Suggestion not found.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(text))
                candidate = text;
            else if (index is >= 0 && index < set.Candidates.Count)
                candidate = set.Candidates[index.Value];

            if (string.IsNullOrWhiteSpace(candidate))
            {
                error = "Invalid suggestion index.";
                return false;
            }

            _suggestions.Remove(set);
        }

        var message = set!.ReplyPrefix + SanitizeForChat(candidate!);
        if (message.Length <= set.ReplyPrefix.Length)
        {
            error = "The reply is empty.";
            return false;
        }

        _ = _plugin.Framework.RunOnTick(() => _sender.SendMessage(message));
        SuggestionRemoved?.Invoke(id);
        error = "";
        return true;
    }

    public void DismissSuggestion(string id)
    {
        bool removed;
        lock (_gate)
        {
            removed = _suggestions.RemoveAll(s => s.Id == id) > 0;
        }
        if (removed)
            SuggestionRemoved?.Invoke(id);
    }

    /// <summary>Cancels in-flight requests and clears session state on logout.</summary>
    public void HandleLogout()
    {
        lock (_gate)
        {
            foreach (var cts in _requestCts.Values)
            {
                try { cts.Cancel(); } catch { /* already disposed */ }
            }
            _requestCts.Clear();
            _inFlight.Clear();
            _lastTrigger.Clear();
            _recentTriggers.Clear();
            _suggestions.Clear();
            _activity.Clear();
        }

        lock (_contextGate)
        {
            _contextBuffer.Clear();
        }

        Interlocked.Exchange(ref _liveMessageCount, 0);
        _backfillDone = false;
    }

    // ── Triggering ──────────────────────────────────────────────────

    private void OnAnyMessageReceived(ReceivedChatMessage message)
    {
        // Count every event, including text-less ones: the game stores one
        // record per event, and the backfill boundary relies on that.
        Interlocked.Increment(ref _liveMessageCount);

        if (string.IsNullOrWhiteSpace(ExtractText(message)))
            return;

        lock (_contextGate)
        {
            _contextBuffer.AddLast(message);
            while (_contextBuffer.Count > ContextBufferSize)
                _contextBuffer.RemoveFirst();
        }
    }

    /// <summary>
    /// Recovers the game's own stored chat log, including messages that were
    /// printed before this plugin instance loaded (e.g. right after an update).
    /// Safe to call every frame; only the first successful call does any work.
    /// </summary>
    public void BackfillContextLog()
    {
        if (_backfillDone)
            return;

        var live = Volatile.Read(ref _liveMessageCount);
        if (!GameChatLog.TryReadRecent(
                ContextBufferSize + live,
                _plugin.LocalPlayerName,
                _plugin.LocalPlayerWorld,
                _log,
                out var stored))
        {
            return;
        }

        _backfillDone = true;

        // The newest `live` records are the ones this instance already received
        // through Dalamud; drop them so they are not added twice.
        if (live > 0)
        {
            if (live >= stored.Count)
                stored.Clear();
            else
                stored.RemoveRange(stored.Count - live, live);
        }

        if (stored.Count == 0)
            return;

        var added = 0;
        var addedMessages = new List<ReceivedChatMessage>();
        lock (_contextGate)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var message in _contextBuffer)
                seen.Add(ContextKey(message));

            // Prepend oldest-first while keeping the existing live messages last.
            for (var index = stored.Count - 1; index >= 0; index--)
            {
                var message = stored[index];
                if (!seen.Add(ContextKey(message)))
                    continue;

                _contextBuffer.AddFirst(message);
                addedMessages.Add(message);
                added++;
            }

            while (_contextBuffer.Count > ContextBufferSize)
                _contextBuffer.RemoveFirst();
        }

        // The web UI and auto-reply history use ChatHistory; restore tracked
        // channels there too, oldest first.
        addedMessages.Reverse();
        foreach (var message in addedMessages)
        {
            if (ChatHistory.TrackedTypes.Contains((XivChatType)message.Type))
                _receiver.History.Add(message);
        }

        _log.Info($"[AI] Backfilled {added} message(s) from the game chat log.");
    }

    private static string ContextKey(ReceivedChatMessage message) =>
        $"{message.Timestamp / 1000}|{message.Type}|{message.SenderName}|{ExtractText(message)}";

    private List<ReceivedChatMessage> GetContextMessages(IReadOnlySet<ushort> types, int limit)
    {
        if (types.Count == 0)
            return [];

        lock (_contextGate)
        {
            return _contextBuffer
                .Where(m => types.Contains(m.Type))
                .TakeLast(Math.Clamp(limit, 1, 100))
                .ToList();
        }
    }

    private void OnMessageReceived(ReceivedChatMessage message)
    {
        var config = _plugin.Config;
        if (!config.AiEnabled)
            return;

        var type = message.Type;
        if (!AiChannels.IsChatType(type))
            return;
        if (type == (ushort)XivChatType.TellOutgoing)
            return;
        if (string.IsNullOrEmpty(_plugin.LocalPlayerName))
            return;
        if (string.IsNullOrWhiteSpace(message.SenderName))
            return;
        if (IsLocalPlayer(message.SenderName))
            return;

        var text = ExtractText(message);
        if (string.IsNullOrWhiteSpace(text))
            return;

        foreach (var rule in config.AiRules)
        {
            if (!rule.Enabled || rule.ChannelTypes.Count == 0)
                continue;
            if (!rule.ChannelTypes.Contains(type))
                continue;

            if (TryReserveTrigger(rule))
                _ = Task.Run(() => GenerateAsync(rule, message, text));
        }
    }

    private bool TryReserveTrigger(AiTriggerRule rule)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (_inFlight.Contains(rule.Id))
                return false;

            if (_lastTrigger.TryGetValue(rule.Id, out var last)
                && (now - last).TotalSeconds < Math.Max(0, rule.CooldownSeconds))
            {
                return false;
            }

            if (!_recentTriggers.TryGetValue(rule.Id, out var timestamps))
            {
                timestamps = [];
                _recentTriggers[rule.Id] = timestamps;
            }

            timestamps.RemoveAll(t => (now - t).TotalSeconds >= 60);
            if (timestamps.Count >= Math.Max(1, rule.MaxPerMinute))
                return false;

            timestamps.Add(now);
            _lastTrigger[rule.Id] = now;
            _inFlight.Add(rule.Id);
            return true;
        }
    }

    private async Task GenerateAsync(AiTriggerRule rule, ReceivedChatMessage trigger, string triggerText)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        lock (_gate) _requestCts[rule.Id] = cts;

        try
        {
            await _concurrency.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                await GenerateCoreAsync(rule, trigger, triggerText, cts.Token).ConfigureAwait(false);
            }
            finally
            {
                _concurrency.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Plugin unloading or logout — nothing to report.
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[AI] Failed to generate a reply.");
            AddActivity("error", ex.Message);
        }
        finally
        {
            lock (_gate)
            {
                _inFlight.Remove(rule.Id);
                _requestCts.Remove(rule.Id);
            }
        }
    }

    private async Task GenerateCoreAsync(
        AiTriggerRule rule,
        ReceivedChatMessage trigger,
        string triggerText,
        CancellationToken cancellationToken)
    {
        var config = _plugin.Config;
        var agent = config.AiAgents.FirstOrDefault(a => a.Id == rule.AgentId)
                    ?? config.AiAgents.FirstOrDefault();

        if (agent == null)
        {
            AddActivity("error", "No AI agent is configured.");
            return;
        }

        var model = string.IsNullOrWhiteSpace(agent.Model) ? config.AiDefaultModel : agent.Model;
        var messages = BuildContextMessages(rule, trigger, triggerText);
        var systemPrompt = BuildSystemPrompt(agent, rule);

        messages.Insert(0, new AiChatMessage { Role = "system", Content = systemPrompt });

        async Task<string> CallApiAsync(IReadOnlyList<AiChatMessage> requestMessages)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.AiRequestTimeoutSeconds, 5, 120)));

            return await _client.CompleteAsync(
                config.AiApiBaseUrl,
                config.AiApiKey,
                model,
                requestMessages,
                agent.Temperature,
                agent.MaxTokens,
                timeout.Token).ConfigureAwait(false);
        }

        var content = await CallApiAsync(messages);

        // Some models ignore the JSON-only instruction on the first attempt.
        // Retry once with an explicit correction before showing anything to the user.
        if (rule.Mode == AiReplyModes.Options && !LooksLikeJsonArray(content))
        {
            var retryMessages = new List<AiChatMessage>(messages)
            {
                new()
                {
                    Role = "user",
                    Content = "Your previous response was not a valid JSON array. " +
                              "Return ONLY a JSON array of strings, for example [\"reply one\", \"reply two\"]. " +
                              "Do not include explanations, analysis, numbering or code fences.",
                },
            };
            content = await CallApiAsync(retryMessages);
        }

        switch (rule.Mode)
        {
            case AiReplyModes.Options:
            {
                if (!LooksLikeJsonArray(content))
                {
                    AddActivity("error", "The model did not return a JSON array of reply options; skipped.");
                    return;
                }

                var candidates = AiClient.ParseCandidates(content, rule.OptionsCount);
                AddSuggestion(rule, agent, trigger, triggerText, candidates);
                break;
            }

            case AiReplyModes.Suggestion:
            {
                if (LooksLikeAnalysis(content))
                {
                    AddActivity("error", "The model returned analysis instead of a reply; skipped.");
                    return;
                }

                var suggestion = SanitizeForChat(content);
                if (suggestion.Length == 0)
                {
                    AddActivity("error", "The model returned an empty suggestion.");
                    return;
                }
                AddSuggestion(rule, agent, trigger, triggerText, new[] { suggestion });
                break;
            }

            default:
            {
                if (LooksLikeAnalysis(content))
                {
                    AddActivity("error", "The model returned analysis instead of a reply; skipped.");
                    return;
                }

                var reply = SanitizeForChat(content);
                if (reply.Length == 0)
                {
                    AddActivity("error", "The model returned an empty reply.");
                    return;
                }

                var prefix = BuildReplyPrefix(trigger);
                if (prefix.Length == 0)
                {
                    AddActivity("error", $"No reply channel is available for {AiChannels.GetLabel(trigger.Type)}.");
                    return;
                }

                var fullMessage = prefix + reply;
                _ = _plugin.Framework.RunOnTick(() => _sender.SendMessage(fullMessage));
                AddActivity("auto-reply", $"{BuildSenderLabel(trigger)}: {reply}");
                break;
            }
        }
    }

    // ── Prompt / context construction ───────────────────────────────

    private List<AiChatMessage> BuildContextMessages(
        AiTriggerRule rule,
        ReceivedChatMessage trigger,
        string triggerText)
    {
        var config = _plugin.Config;
        var limit = Math.Clamp(config.AiContextMessageCount, 1, 100);
        var types = rule.ChannelTypes
            .Where(AiChannels.IsChatType)
            .Select(t => (XivChatType)t)
            .ToHashSet();

        if (types.Count == 0)
            types = new HashSet<XivChatType> { (XivChatType)trigger.Type };

        var history = _receiver.History.GetRecentByTypes(types, Math.Max(limit * 4, 80)).ToList();

        if (trigger.Type == (ushort)XivChatType.TellIncoming)
            history = history.Where(m => IsTellPartnerOf(m, trigger)).ToList();

        var recent = history.TakeLast(limit).ToList();

        // Ensure the triggering message is the final turn even if history ordering
        // or context truncation dropped it.
        if (!recent.Any(m => ReferenceEquals(m, trigger))
            && !recent.Any(m => m.Timestamp == trigger.Timestamp
                                && m.Type == trigger.Type
                                && m.SenderName == trigger.SenderName))
        {
            recent.Add(trigger);
        }

        var messages = new List<AiChatMessage>(recent.Count + 1);
        foreach (var message in recent)
        {
            var text = ExtractText(message);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var isOwn = IsLocalPlayer(message.SenderName);
            var label = AiChannels.GetLabel(message.Type);
            var sender = string.IsNullOrWhiteSpace(message.SenderName) ? "Unknown" : message.SenderName;
            messages.Add(new AiChatMessage
            {
                Role = isOwn ? "assistant" : "user",
                Content = $"[{label}] {sender}{(isOwn ? " (you)" : "")}: {text}",
            });
        }

        var playerName = GetPlayerName();
        var channelLabel = AiChannels.GetLabel(trigger.Type);
        var senderLabel = BuildSenderLabel(trigger);

        var task = rule.Mode switch
        {
            AiReplyModes.Options =>
                $"The latest message from {senderLabel} in {channelLabel} is: \"{triggerText}\".\n" +
                $"Write exactly {Math.Clamp(rule.OptionsCount, 1, 10)} distinct short replies that {playerName} could send next. " +
                "Return ONLY a JSON array of strings. No explanations, no analysis, no questions.",
            AiReplyModes.Suggestion =>
                $"The latest message from {senderLabel} in {channelLabel} is: \"{triggerText}\".\n" +
                $"Write exactly ONE short suggested reply that {playerName} could send. Output ONLY the reply text.",
            _ =>
                $"The latest message from {senderLabel} in {channelLabel} is: \"{triggerText}\".\n" +
                $"Reply now as {playerName}. Output ONLY the chat message.",
        };

        messages.Add(new AiChatMessage { Role = "user", Content = task });
        return messages;
    }

    private string BuildSystemPrompt(AiAgentConfig agent, AiTriggerRule rule)
    {
        var isEnglish = string.Equals(_plugin.Config.UiLanguage, "en", StringComparison.OrdinalIgnoreCase);
        var fallback = isEnglish
            ? "You are a friendly, concise participant in the MMORPG Final Fantasy XIV."
            : "你是一名友好、简洁的《最终幻想14》玩家。";

        var playerName = GetPlayerName();
        var builder = new StringBuilder(
            string.IsNullOrWhiteSpace(agent.SystemPrompt) ? fallback : agent.SystemPrompt.Trim());
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine($"You are writing an in-game chat message as the FFXIV player character \"{playerName}\".");
        builder.AppendLine($"Messages from \"{playerName}\" in the transcript are your own previous messages. Never reply to yourself.");
        builder.AppendLine("The transcript is a conversation, not a document to analyse. Never summarize, explain or comment on it.");
        builder.AppendLine("Never ask the user what they want. You are not an assistant in this task; you are just writing the next chat message.");
        builder.AppendLine("Reply in the same language as the latest message. Keep it natural and short.");
        builder.AppendLine("Do not use Markdown, code blocks, bullet points or line breaks. Do not mention being an AI.");

        switch (rule.Mode)
        {
            case AiReplyModes.Options:
            {
                var count = Math.Clamp(rule.OptionsCount, 1, 10);
                builder.AppendLine(
                    $"TASK: Write exactly {count} distinct reply candidates. " +
                    $"Return ONLY a JSON array of {count} strings. " +
                    "No code fences, no explanations, no analysis, no questions.");
                break;
            }

            case AiReplyModes.Suggestion:
                builder.AppendLine(
                    "TASK: Write exactly ONE suggested reply. " +
                    "Output ONLY the reply text. No explanations, no analysis, no questions.");
                break;

            default:
                builder.AppendLine(
                    "TASK: Write exactly ONE reply. " +
                    "Output ONLY the reply text. No explanations, no analysis, no questions.");
                break;
        }

        return builder.ToString();
    }

    // ── Manual conversation ─────────────────────────────────────────

    /// <summary>
    /// Answers a free-form message from the player, optionally with recent
    /// messages from the selected game channels as context.
    /// </summary>
    public async Task<AiChatResult> ChatAsync(
        string? agentId,
        string userMessage,
        IReadOnlyList<string> contextScopes,
        IReadOnlyList<AiChatTurn> conversation,
        CancellationToken cancellationToken)
    {
        var config = _plugin.Config;
        if (!config.AiEnabled)
            throw new InvalidOperationException("The AI assistant is disabled.");
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new InvalidOperationException("The message is empty.");

        var agent = config.AiAgents.FirstOrDefault(a => a.Id == agentId)
                    ?? config.AiAgents.FirstOrDefault()
                    ?? throw new InvalidOperationException("No AI agent is configured.");

        var model = string.IsNullOrWhiteSpace(agent.Model) ? config.AiDefaultModel : agent.Model;
        var (contextText, contextCount) = BuildChannelContext(contextScopes);
        _log.Debug(
            $"[AI] Manual chat context: {contextCount} message(s) from " +
            $"[{string.Join(", ", contextScopes)}].");

        var messages = new List<AiChatMessage>
        {
            new() { Role = "system", Content = BuildChatSystemPrompt(agent) },
        };

        foreach (var turn in conversation.TakeLast(20))
        {
            var content = turn.Content?.Trim() ?? "";
            if (content.Length == 0)
                continue;
            if (content.Length > 4000)
                content = content[..4000];

            messages.Add(new AiChatMessage
            {
                Role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? "assistant"
                    : "user",
                Content = content,
            });
        }

        // The transcript lives in the user turn rather than the system prompt:
        // some OpenAI-compatible providers truncate or ignore system messages.
        messages.Add(new AiChatMessage
        {
            Role = "user",
            Content = ComposeChatUserMessage(userMessage.Trim(), contextText),
        });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.AiRequestTimeoutSeconds, 5, 120)));

        await _concurrency.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var reply = await _client.CompleteAsync(
                config.AiApiBaseUrl,
                config.AiApiKey,
                model,
                messages,
                agent.Temperature,
                agent.MaxTokens,
                timeout.Token).ConfigureAwait(false);

            var trimmed = reply.Trim();
            if (trimmed.Length == 0)
                throw new InvalidOperationException("The model returned an empty reply.");
            return new AiChatResult { Reply = trimmed, ContextMessageCount = contextCount };
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private string BuildChatSystemPrompt(AiAgentConfig agent)
    {
        var isEnglish = string.Equals(_plugin.Config.UiLanguage, "en", StringComparison.OrdinalIgnoreCase);
        var fallback = isEnglish
            ? "You are a helpful assistant embedded in the Linkpearl plugin for Final Fantasy XIV."
            : "你是 Linkpearl 插件内置的《最终幻想14》聊天助手。";
        var playerName = GetPlayerName();

        var builder = new StringBuilder(
            string.IsNullOrWhiteSpace(agent.SystemPrompt) ? fallback : agent.SystemPrompt.Trim());
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(
            $"You are talking directly to the player. Their character name is \"{playerName}\"; " +
            "messages from this name in the game context are the player's own words.");
        builder.AppendLine(
            "The player's message may include a transcript of recent in-game chat from the selected channels/categories. " +
            "Use it as reference to answer the player's question; do not reply to the transcript as a participant " +
            "unless the player explicitly asks you to draft a reply.");
        builder.AppendLine(
            "Answer in the same language as the player's latest message. " +
            "Be concise and practical. Markdown is allowed when it improves readability.");
        builder.AppendLine(
            "If the transcript does not contain the information needed, say so plainly instead of guessing.");

        return builder.ToString();
    }

    /// <summary>
    /// Builds the transcript block for a manual chat turn. "all" means every chat
    /// channel tracked in <see cref="ChatHistory"/> (the same feed the web UI shows);
    /// the extra categories (battle/system/loot/NPC/craft) read from the
    /// all-message buffer instead.
    /// </summary>
    private (string Text, int Count) BuildChannelContext(IReadOnlyList<string> contextScopes)
    {
        if (contextScopes.Count == 0)
            return ("", 0);

        var includeAllChat = contextScopes.Any(scope =>
            string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase));
        var selectedTypes = new HashSet<ushort>();

        foreach (var scope in contextScopes)
        {
            if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
                continue;

            var types = AiChannels.GetContextScopeTypes(scope);
            if (types == null)
                continue;

            foreach (var type in types)
                selectedTypes.Add(type);
        }

        if (!includeAllChat && selectedTypes.Count == 0)
            return ("", 0);

        var limit = Math.Clamp(_plugin.Config.AiContextMessageCount, 1, 100);
        var messages = CollectContextMessages(selectedTypes, includeAllChat, limit)
            .Where(message => !string.IsNullOrWhiteSpace(ExtractText(message)))
            .ToList();

        if (messages.Count == 0)
            return ("", 0);

        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            var text = ExtractText(message);
            var isOwn = IsLocalPlayer(message.SenderName);
            var label = AiChannels.GetLabel(message.Type);
            var sender = string.IsNullOrWhiteSpace(message.SenderName) ? "Unknown" : message.SenderName;
            builder.AppendLine($"[{label}] {sender}{(isOwn ? " (you)" : "")}: {text}");
        }

        return (builder.ToString().TrimEnd(), messages.Count);
    }

    private List<ReceivedChatMessage> CollectContextMessages(
        HashSet<ushort> selectedTypes,
        bool includeAllChat,
        int limit)
    {
        var trackedTypes = new HashSet<XivChatType>();
        var untrackedTypes = new HashSet<ushort>();

        foreach (var type in selectedTypes)
        {
            if (ChatHistory.TrackedTypes.Contains((XivChatType)type))
                trackedTypes.Add((XivChatType)type);
            else
                untrackedTypes.Add(type);
        }

        if (includeAllChat)
        {
            foreach (var type in ChatHistory.TrackedTypes)
                trackedTypes.Add(type);
        }

        var messages = new List<ReceivedChatMessage>();
        if (trackedTypes.Count > 0)
        {
            messages.AddRange(_receiver.History.GetRecentByTypes(trackedTypes, limit));

            // History is cleared on login, while the context buffer (including
            // messages restored after a plugin reload) is not. Merge both.
            var trackedTypeIds = trackedTypes.Select(type => (ushort)type).ToHashSet();
            messages.AddRange(GetContextMessages(trackedTypeIds, limit));
        }
        if (untrackedTypes.Count > 0)
            messages.AddRange(GetContextMessages(untrackedTypes, limit));

        var seen = new HashSet<ReceivedChatMessage>(ReferenceEqualityComparer.Instance);
        var unique = messages.Where(message => seen.Add(message)).ToList();

        // OrderBy is stable, which matters for backfilled lines that share the
        // game log's second-granularity timestamps.
        return unique
            .OrderBy(message => message.Timestamp)
            .TakeLast(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    private static string ComposeChatUserMessage(string userMessage, string context)
    {
        if (string.IsNullOrEmpty(context))
        {
            return "No recent in-game chat messages are available as context. " +
                   "Answer the question below; if it depends on missing chat context, say so instead of guessing.\n\n" +
                   userMessage;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Recent in-game chat transcript (oldest to newest), for reference only:");
        builder.AppendLine(context);
        builder.AppendLine();
        builder.AppendLine(
            "Answer the question below using the transcript when relevant. " +
            "If the transcript does not contain the answer, say so instead of guessing.");
        builder.AppendLine();
        builder.Append(userMessage);
        return builder.ToString();
    }

    private string GetPlayerName() =>
        string.IsNullOrWhiteSpace(_plugin.LocalPlayerName) ? "the player" : _plugin.LocalPlayerName;

    private static bool LooksLikeJsonArray(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var newline = trimmed.IndexOf('\n');
            if (newline >= 0)
                trimmed = trimmed[(newline + 1)..].TrimStart();
        }

        return trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static readonly string[] AnalysisMarkers =
    [
        "你想让我", "需要我做什么", "这段内容", "这段聊天", "这是一段",
        "what would you like me to", "what do you want me to",
        "i'm not sure what you want", "i am not sure what you want",
        "this appears to be a", "this looks like a chat",
    ];

    /// <summary>Detects the common "model analyses the transcript instead of replying" failure.</summary>
    private static bool LooksLikeAnalysis(string content)
    {
        var lower = content.ToLowerInvariant();
        return AnalysisMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    // ── Suggestion / activity storage ───────────────────────────────

    private void AddSuggestion(
        AiTriggerRule rule,
        AiAgentConfig agent,
        ReceivedChatMessage trigger,
        string triggerText,
        IEnumerable<string> candidates)
    {
        var list = candidates
            .Select(SanitizeForChat)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Clamp(rule.OptionsCount, 1, 10))
            .ToList();

        if (list.Count == 0)
        {
            AddActivity("error", "The model returned no usable candidates.");
            return;
        }

        var set = new AiSuggestionSet
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = rule.Id,
            RuleName = rule.Name,
            AgentName = agent.Name,
            Mode = rule.Mode,
            ChannelType = trigger.Type,
            ChannelLabel = AiChannels.GetLabel(trigger.Type),
            SenderName = trigger.SenderName,
            SenderWorld = trigger.SenderWorld,
            SourceText = triggerText,
            Candidates = list,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReplyPrefix = BuildReplyPrefix(trigger),
        };

        lock (_gate)
        {
            _suggestions.Insert(0, set);
            while (_suggestions.Count > MaxSuggestions)
                _suggestions.RemoveAt(_suggestions.Count - 1);
        }

        SuggestionAdded?.Invoke(set);
    }

    private void AddActivity(string kind, string message)
    {
        var entry = new AiActivityEntry
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Kind = kind,
            Message = message,
        };

        lock (_gate)
        {
            _activity.Insert(0, entry);
            if (_activity.Count > MaxActivity)
                _activity.RemoveAt(_activity.Count - 1);
        }

        ActivityAdded?.Invoke(entry);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private bool IsLocalPlayer(string name) =>
        !string.IsNullOrEmpty(_plugin.LocalPlayerName)
        && string.Equals(name, _plugin.LocalPlayerName, StringComparison.OrdinalIgnoreCase);

    private static string BuildSenderLabel(ReceivedChatMessage message) =>
        string.IsNullOrWhiteSpace(message.SenderName) ? "Unknown" : message.SenderName;

    private static string BuildReplyPrefix(ReceivedChatMessage message)
    {
        if (message.Type == (ushort)XivChatType.TellIncoming)
        {
            var target = string.IsNullOrWhiteSpace(message.SenderWorld)
                ? message.SenderName
                : $"{message.SenderName}@{message.SenderWorld}";
            return $"/tell {target} ";
        }

        return AiChannels.GetReplyPrefix(message.Type);
    }

    private static bool IsTellPartnerOf(ReceivedChatMessage message, ReceivedChatMessage trigger)
    {
        if (message.Type == (ushort)XivChatType.TellIncoming)
            return NamesMatch(message.SenderName, message.SenderWorld, trigger.SenderName, trigger.SenderWorld);

        if (message.Type == (ushort)XivChatType.TellOutgoing)
            return NamesMatch(message.RecipientName ?? "", message.RecipientWorld,
                              trigger.SenderName, trigger.SenderWorld);

        return false;
    }

    private static bool NamesMatch(string nameA, string? worldA, string nameB, string? worldB)
    {
        if (!string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(worldA) && !string.IsNullOrEmpty(worldB))
            return string.Equals(worldA, worldB, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static string ExtractText(ReceivedChatMessage message)
    {
        if (message.MessagePayloads.Count == 0)
            return "";

        var builder = new StringBuilder();
        foreach (var payload in message.MessagePayloads)
        {
            if (payload.Type == "text" && !string.IsNullOrEmpty(payload.Text))
                builder.Append(payload.Text);
        }
        return builder.ToString().Trim();
    }

    /// <summary>Collapses whitespace, strips wrapping quotes, and enforces the chat byte limit.</summary>
    private static string SanitizeForChat(string text)
    {
        var value = Regex.Replace(text, @"\s+", " ").Trim();
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1].Trim();
        }

        if (Encoding.UTF8.GetByteCount(value) <= MaxReplyBytes)
            return value;

        var length = value.Length;
        while (length > 0 && Encoding.UTF8.GetByteCount(value[..length]) > MaxReplyBytes)
            length--;

        return value[..length].TrimEnd();
    }

    public void Dispose()
    {
        _receiver.OnMessageReceived -= OnMessageReceived;
        _receiver.OnAnyMessageReceived -= OnAnyMessageReceived;

        try { _lifetime.Cancel(); } catch { /* ignore */ }
        lock (_gate)
        {
            foreach (var cts in _requestCts.Values)
            {
                try { cts.Cancel(); } catch { /* ignore */ }
            }
            _requestCts.Clear();
        }

        _lifetime.Dispose();
        _concurrency.Dispose();
        _client.Dispose();
    }
}
