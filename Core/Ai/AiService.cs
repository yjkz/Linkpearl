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

    private readonly Plugin _plugin;
    private readonly ChatReceiver _receiver;
    private readonly ChatSender _sender;
    private readonly IPluginLog _log;
    private readonly AiClient _client;

    private readonly List<AiSuggestionSet> _suggestions = [];
    private readonly List<AiActivityEntry> _activity = [];
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
    }

    // ── Triggering ──────────────────────────────────────────────────

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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.AiRequestTimeoutSeconds, 5, 120)));

        var content = await _client.CompleteAsync(
            config.AiApiBaseUrl,
            config.AiApiKey,
            model,
            messages,
            agent.Temperature,
            agent.MaxTokens,
            timeout.Token).ConfigureAwait(false);

        switch (rule.Mode)
        {
            case AiReplyModes.Options:
            {
                var candidates = AiClient.ParseCandidates(content, rule.OptionsCount);
                AddSuggestion(rule, agent, trigger, triggerText, candidates);
                break;
            }

            case AiReplyModes.Suggestion:
            {
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

        var messages = new List<AiChatMessage>(recent.Count);
        foreach (var message in recent)
        {
            var text = ExtractText(message);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var role = IsLocalPlayer(message.SenderName) ? "assistant" : "user";
            var label = AiChannels.GetLabel(message.Type);
            var sender = string.IsNullOrWhiteSpace(message.SenderName) ? "Unknown" : message.SenderName;
            messages.Add(new AiChatMessage
            {
                Role = role,
                Content = $"[{label}] {sender}: {text}",
            });
        }

        if (messages.Count == 0 || messages[^1].Role == "assistant")
        {
            messages.Add(new AiChatMessage
            {
                Role = "user",
                Content = $"[{AiChannels.GetLabel(trigger.Type)}] {BuildSenderLabel(trigger)}: {triggerText}",
            });
        }

        return messages;
    }

    private string BuildSystemPrompt(AiAgentConfig agent, AiTriggerRule rule)
    {
        var isEnglish = string.Equals(_plugin.Config.UiLanguage, "en", StringComparison.OrdinalIgnoreCase);
        var fallback = isEnglish
            ? "You are a friendly, concise assistant in the MMORPG Final Fantasy XIV. Reply naturally based on the conversation."
            : "你是一名友好、简洁的《最终幻想14》游戏聊天助手。请根据对话上下文自然地回复。";

        var prompt = string.IsNullOrWhiteSpace(agent.SystemPrompt) ? fallback : agent.SystemPrompt.Trim();
        var builder = new StringBuilder(prompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine(isEnglish
            ? "You are replying inside the game chat through the Linkpearl plugin."
            : "你正在通过 Linkpearl 插件在游戏内聊天中回复。");
        builder.AppendLine(isEnglish
            ? "Keep the reply short (under 300 characters), natural, and suitable for in-game chat."
            : "回复要简短（300 字符以内）、自然，适合游戏内聊天。");
        builder.AppendLine(isEnglish
            ? "Do not use Markdown, code blocks, or line breaks. Do not mention that you are an AI unless asked."
            : "不要使用 Markdown、代码块或换行。除非被问到，否则不要提到自己是 AI。");

        if (rule.Mode == AiReplyModes.Options)
        {
            var count = Math.Clamp(rule.OptionsCount, 1, 10);
            builder.AppendLine(isEnglish
                ? $"Return exactly {count} distinct reply candidates as a JSON array of strings and nothing else."
                : $"请返回 {count} 条不同的候选回复，使用 JSON 字符串数组格式，不要输出其他内容。");
        }

        return builder.ToString();
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
