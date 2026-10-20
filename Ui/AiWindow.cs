using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Linkpearl.Core.Ai;

namespace Linkpearl.Ui;

public sealed class AiWindow : Window
{
    private readonly Plugin _plugin;

    // ── Settings draft ──────────────────────────────────────────────
    private bool _editEnabled;
    private string _editBaseUrl = "";
    private string _editApiKey = "";
    private string _editModel = "";
    private int _editContextCount = 20;
    private int _editTimeout = 60;
    private string _editLanguage = "zh";
    private List<AiAgentConfig> _editAgents = [];
    private List<AiTriggerRule> _editRules = [];
    private int _selectedAgent;
    private int _selectedRule;

    // ── Connection test state ───────────────────────────────────────
    private volatile bool _testInProgress;
    private volatile string? _testMessage;
    private volatile bool _testOk;

    // ── Per-suggestion edit buffers (suggestion mode) ───────────────
    private readonly Dictionary<string, string> _suggestionDrafts = new();

    // ── Manual conversation state ───────────────────────────────────
    private readonly List<AiChatTurn> _chatTurns = new();
    private readonly object _chatGate = new();
    private readonly HashSet<string> _chatScopes = new(StringComparer.OrdinalIgnoreCase);
    private string _chatInput = "";
    private int _chatAgentIndex;
    private volatile bool _chatSending;
    private volatile string? _chatError;
    private volatile int _lastChatContextCount = -1;
    private static readonly Vector4 ColorYellow = new(1f, 0.85f, 0.2f, 1f);

    private static readonly Vector4 ColorGreen = new(0.4f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 ColorRed = new(0.9f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 ColorMuted = new(0.6f, 0.6f, 0.6f, 1f);

    public AiWindow(Plugin plugin)
        : base($"{UiStrings.T(plugin.Config.UiLanguage, "window.aiTitle")}###linkpearl-ai")
    {
        _plugin = plugin;
        Size = new Vector2(780, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private string T(string key) => UiStrings.T(_plugin.Config.UiLanguage, key);

    private void UpdateWindowTitle()
    {
        var title = $"{T("window.aiTitle")}###linkpearl-ai";
        if (WindowName != title)
            WindowName = title;
    }

    public override void OnOpen()
    {
        SyncFromConfig();
    }

    public override void Draw()
    {
        UpdateWindowTitle();

        if (!ImGui.BeginTabBar("##ai-tabs"))
            return;

        if (ImGui.BeginTabItem(T("ai.tab.suggestions")))
        {
            DrawSuggestionsTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(T("ai.tab.chat")))
        {
            DrawChatTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(T("ai.tab.settings")))
        {
            DrawSettingsTab();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    // ── Suggestions tab ─────────────────────────────────────────────

    private void DrawSuggestionsTab()
    {
        var suggestions = _plugin.Ai.GetSuggestions();
        var activity = _plugin.Ai.GetActivity();

        if (suggestions.Count == 0)
            ImGui.TextColored(ColorMuted, T("ai.noSuggestions"));

        var activeIds = new HashSet<string>(suggestions.Select(s => s.Id));
        foreach (var stale in _suggestionDrafts.Keys.Where(id => !activeIds.Contains(id)).ToList())
            _suggestionDrafts.Remove(stale);

        foreach (var set in suggestions)
        {
            ImGui.PushID(set.Id);

            var channel = UiStrings.T(_plugin.Config.UiLanguage, AiChannels.GetLabelKey(set.ChannelType));
            ImGui.TextColored(ColorMuted, $"[{channel}] {set.SenderName}: {set.SourceText}");
            ImGui.TextColored(ColorMuted,
                $"{T("ai.ruleLabel")}: {set.RuleName}   {T("ai.agentLabel")}: {set.AgentName}   {T("ai.modeLabel")}: {ModeLabel(set.Mode)}");

            if (set.Mode == AiReplyModes.Options)
            {
                for (var i = 0; i < set.Candidates.Count; i++)
                {
                    ImGui.PushID(i);
                    ImGui.TextWrapped(set.Candidates[i]);
                    if (ImGui.SmallButton(T("ai.send")))
                        SendSuggestion(set.Id, i, null);
                    ImGui.PopID();
                }
            }
            else
            {
                if (!_suggestionDrafts.TryGetValue(set.Id, out var draft))
                {
                    draft = set.Candidates.FirstOrDefault() ?? "";
                    _suggestionDrafts[set.Id] = draft;
                }

                if (ImGui.InputTextMultiline("##suggestion-text", ref draft, 4096, new Vector2(-1, 60)))
                    _suggestionDrafts[set.Id] = draft;

                if (ImGui.SmallButton(T("ai.send")))
                    SendSuggestion(set.Id, null, draft);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton(T("ai.dismiss")))
            {
                _plugin.Ai.DismissSuggestion(set.Id);
                _suggestionDrafts.Remove(set.Id);
            }

            ImGui.Separator();
            ImGui.PopID();
        }

        if (ImGui.CollapsingHeader(T("ai.activity")))
        {
            if (activity.Count == 0)
                ImGui.TextColored(ColorMuted, T("ai.noActivity"));

            for (var i = 0; i < Math.Min(activity.Count, 20); i++)
            {
                var entry = activity[i];
                var color = entry.Kind switch
                {
                    "error" => ColorRed,
                    "auto-reply" => ColorGreen,
                    _ => ColorMuted,
                };
                ImGui.TextColored(color, entry.Message);
            }
        }
    }

    private void SendSuggestion(string id, int? index, string? text)
    {
        if (!_plugin.Ai.TrySendSuggestion(id, index, text, out var error))
            _plugin.Log.Warning($"[AI] {error}");
    }

    private string ModeLabel(string mode) => mode switch
    {
        AiReplyModes.Options => T("ai.rule.mode.options"),
        AiReplyModes.Suggestion => T("ai.rule.mode.suggestion"),
        _ => T("ai.rule.mode.auto"),
    };

    // ── Manual chat tab ─────────────────────────────────────────────

    private void DrawChatTab()
    {
        var config = _plugin.Config;

        if (!config.AiEnabled)
            ImGui.TextColored(ColorYellow, T("ai.disabledHint"));

        if (config.AiAgents.Count == 0)
        {
            ImGui.TextColored(ColorMuted, T("ai.noAgents"));
            return;
        }

        _chatAgentIndex = Math.Clamp(_chatAgentIndex, 0, config.AiAgents.Count - 1);
        var agentNames = config.AiAgents.Select(a => a.Name).ToArray();
        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        ImGui.Combo(T("ai.chat.agent") + "##chat-agent", ref _chatAgentIndex, agentNames, agentNames.Length);

        if (ImGui.CollapsingHeader(T("ai.chat.contextChannels")))
        {
            ImGui.TextColored(ColorMuted, T("ai.chat.categories"));
            foreach (var (key, labelKey, _) in AiChannels.ChatContextCategories)
            {
                var selected = _chatScopes.Contains(key);
                var label = UiStrings.T(config.UiLanguage, labelKey);
                if (ImGui.Checkbox($"{label}##ctx-cat-{key}", ref selected))
                {
                    _lastChatContextCount = -1;
                    if (selected)
                        _chatScopes.Add(key);
                    else
                        _chatScopes.Remove(key);
                }
            }

            ImGui.Separator();
            ImGui.TextColored(ColorMuted, T("ai.chat.channels"));

            foreach (var (key, labelKey, _) in AiChannels.ChatContextChannels)
            {
                var selected = _chatScopes.Contains(key);
                var label = UiStrings.T(config.UiLanguage, labelKey);
                if (ImGui.Checkbox($"{label}##ctx-ch-{key}", ref selected))
                {
                    _lastChatContextCount = -1;
                    if (selected)
                        _chatScopes.Add(key);
                    else
                        _chatScopes.Remove(key);
                }
            }

            ImGui.TextColored(ColorMuted, T("ai.chat.contextHint"));
            ImGui.Spacing();
        }

        if (_chatScopes.Count > 0 && _lastChatContextCount == 0)
            ImGui.TextColored(ColorYellow, T("ai.chat.noContext"));
        else if (_chatScopes.Count > 0 && _lastChatContextCount > 0)
            ImGui.TextColored(ColorMuted, $"{T("ai.chat.contextAttached")} {_lastChatContextCount}");

        var available = ImGui.GetContentRegionAvail();
        var historyHeight = Math.Max(140, available.Y - 96);
        if (ImGui.BeginChild("##chat-history", new Vector2(0, historyHeight)))
        {
            List<AiChatTurn> history;
            lock (_chatGate)
                history = _chatTurns.ToList();

            if (history.Count == 0 && !_chatSending)
                ImGui.TextColored(ColorMuted, T("ai.chat.empty"));

            foreach (var turn in history)
            {
                var isUser = string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase);
                ImGui.TextColored(isUser ? ColorGreen : ColorMuted,
                    isUser ? T("ai.chat.you") : T("ai.chat.assistant"));
                ImGui.TextWrapped(turn.Content);
                ImGui.Separator();
            }

            if (_chatSending)
                ImGui.TextColored(ColorYellow, T("ai.chat.thinking"));

            ImGui.EndChild();
        }

        if (!string.IsNullOrEmpty(_chatError))
            ImGui.TextColored(ColorRed, $"{T("ai.chat.error")}: {_chatError}");

        ImGui.InputTextMultiline("##chat-input", ref _chatInput, 4096, new Vector2(0, 56));

        using (ImRaii.Disabled(_chatSending || !config.AiEnabled || string.IsNullOrWhiteSpace(_chatInput)))
        {
            if (ImGui.Button(T("ai.chat.send")))
                StartChatRequest();
        }

        ImGui.SameLine();

        if (ImGui.Button(T("ai.chat.clear")))
        {
            lock (_chatGate)
                _chatTurns.Clear();
            _chatError = null;
        }
    }

    private void StartChatRequest()
    {
        var message = _chatInput.Trim();
        if (message.Length == 0 || _chatSending)
            return;

        var config = _plugin.Config;
        if (config.AiAgents.Count == 0)
            return;

        var agent = config.AiAgents[Math.Clamp(_chatAgentIndex, 0, config.AiAgents.Count - 1)];
        var scopes = _chatScopes.ToList();

        List<AiChatTurn> history;
        lock (_chatGate)
        {
            history = _chatTurns.ToList();
            _chatTurns.Add(new AiChatTurn { Role = "user", Content = message });
        }

        _chatInput = "";
        _chatError = null;
        _chatSending = true;
        _lastChatContextCount = -1;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _plugin.Ai
                    .ChatAsync(agent.Id, message, scopes, history, CancellationToken.None)
                    .ConfigureAwait(false);

                lock (_chatGate)
                    _chatTurns.Add(new AiChatTurn { Role = "assistant", Content = result.Reply });
                _lastChatContextCount = result.ContextMessageCount;
            }
            catch (Exception ex)
            {
                _chatError = ex.Message;
            }
            finally
            {
                _chatSending = false;
            }
        });
    }

    // ── Settings tab ────────────────────────────────────────────────

    private void DrawSettingsTab()
    {
        DrawGlobalSettings();
        ImGui.Separator();
        DrawAgentsSettings();
        ImGui.Separator();
        DrawRulesSettings();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button(T("common.save")))
            Save();
        ImGui.SameLine();
        if (ImGui.Button(T("common.discard")))
            SyncFromConfig();
    }

    private void DrawGlobalSettings()
    {
        ImGui.TextColored(ColorMuted, T("ai.section.global"));
        ImGui.Spacing();

        ImGui.Checkbox(T("ai.enabled"), ref _editEnabled);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText(T("ai.baseUrl"), ref _editBaseUrl, 512);

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText(T("ai.apiKey"), ref _editApiKey, 512, ImGuiInputTextFlags.Password);
        ImGui.TextColored(ColorMuted,
            string.IsNullOrEmpty(_plugin.Config.AiApiKey) ? "" : T("ai.apiKeyKeep"));

        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        ImGui.InputText(T("ai.defaultModel"), ref _editModel, 128);

        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt(T("ai.contextCount"), ref _editContextCount);
        _editContextCount = Math.Clamp(_editContextCount, 1, 100);

        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputInt(T("ai.timeout"), ref _editTimeout);
        _editTimeout = Math.Clamp(_editTimeout, 5, 120);

        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        var languageLabels = new[] { T("language.zh"), T("language.en") };
        var languageIndex = _editLanguage == "en" ? 1 : 0;
        if (ImGui.Combo(T("section.language") + "##ai-language", ref languageIndex, languageLabels, languageLabels.Length))
        {
            _editLanguage = languageIndex == 1 ? "en" : "zh";
            _plugin.Config.UiLanguage = _editLanguage;
            _plugin.SaveConfig();
            _plugin.Server.NotifyUiLanguageChanged();
        }

        using (ImRaii.Disabled(_testInProgress))
        {
            if (ImGui.Button(_testInProgress ? T("ai.testing") : T("ai.test")))
                StartTest();
        }

        if (!string.IsNullOrEmpty(_testMessage))
        {
            ImGui.SameLine();
            ImGui.TextColored(_testOk ? ColorGreen : ColorRed,
                $"{(_testOk ? T("ai.testOk") : T("ai.testFailed"))}: {_testMessage}");
        }
    }

    private void DrawAgentsSettings()
    {
        ImGui.TextColored(ColorMuted, T("ai.section.agents"));
        ImGui.Spacing();

        if (_editAgents.Count == 0)
        {
            ImGui.TextColored(ColorMuted, T("ai.noAgents"));
        }
        else
        {
            _selectedAgent = Math.Clamp(_selectedAgent, 0, _editAgents.Count - 1);
            var names = _editAgents.Select(a => a.Name).ToArray();
            ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
            ImGui.Combo(T("ai.selectAgent") + "##agent-select", ref _selectedAgent, names, names.Length);

            var agent = _editAgents[_selectedAgent];

            var agentName = agent.Name;
            ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText(T("ai.agent.name"), ref agentName, 128))
                agent.Name = agentName;

            var agentModel = agent.Model;
            ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText(T("ai.agent.model"), ref agentModel, 128))
                agent.Model = agentModel;

            var temperature = (float)agent.Temperature;
            ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
            if (ImGui.SliderFloat(T("ai.agent.temperature"), ref temperature, 0f, 2f, "%.2f"))
                agent.Temperature = temperature;

            var maxTokens = agent.MaxTokens;
            ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt(T("ai.agent.maxTokens"), ref maxTokens))
                agent.MaxTokens = Math.Clamp(maxTokens, 1, 4096);

            var systemPrompt = agent.SystemPrompt;
            if (ImGui.InputTextMultiline(T("ai.agent.systemPrompt"), ref systemPrompt, 8192, new Vector2(-1, 120)))
                agent.SystemPrompt = systemPrompt;
        }

        foreach (var preset in AiAgentPresets.All)
        {
            if (ImGui.Button($"{T("ai.preset.add")}「{preset.Name}」##preset-{preset.Id}"))
            {
                _editAgents.Add(AiAgentPresets.CreateAgent(preset, _editAgents));
                _selectedAgent = _editAgents.Count - 1;
            }
            ImGui.SameLine();
        }

        if (ImGui.Button(T("ai.agent.add")))
        {
            _editAgents.Add(new AiAgentConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"Agent {_editAgents.Count + 1}",
                Temperature = 0.7,
                MaxTokens = 300,
            });
            _selectedAgent = _editAgents.Count - 1;
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(_editAgents.Count <= 1 || _editAgents.Count == 0))
        {
            if (ImGui.Button(T("ai.agent.remove")))
            {
                _editAgents.RemoveAt(_selectedAgent);
                _selectedAgent = Math.Clamp(_selectedAgent, 0, Math.Max(0, _editAgents.Count - 1));

                var fallback = _editAgents.FirstOrDefault()?.Id ?? "";
                foreach (var rule in _editRules)
                {
                    if (_editAgents.All(a => a.Id != rule.AgentId))
                        rule.AgentId = fallback;
                }
            }
        }
    }

    private void DrawRulesSettings()
    {
        ImGui.TextColored(ColorMuted, T("ai.section.rules"));
        ImGui.Spacing();

        if (_editRules.Count == 0)
            ImGui.TextColored(ColorMuted, T("ai.noRules"));

        if (_editRules.Count > 0)
        {
            _selectedRule = Math.Clamp(_selectedRule, 0, _editRules.Count - 1);
            var names = _editRules.Select(r => r.Name).ToArray();
            ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
            ImGui.Combo(T("ai.selectRule") + "##rule-select", ref _selectedRule, names, names.Length);

            var rule = _editRules[_selectedRule];

            var ruleEnabled = rule.Enabled;
            if (ImGui.Checkbox(T("ai.rule.enabled"), ref ruleEnabled))
                rule.Enabled = ruleEnabled;

            var ruleName = rule.Name;
            ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText(T("ai.rule.name"), ref ruleName, 128))
                rule.Name = ruleName;

            if (_editAgents.Count > 0)
            {
                var agentNames = _editAgents.Select(a => a.Name).ToArray();
                var agentIndex = Math.Max(0, _editAgents.FindIndex(a => a.Id == rule.AgentId));
                ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo(T("ai.rule.agent"), ref agentIndex, agentNames, agentNames.Length))
                    rule.AgentId = _editAgents[agentIndex].Id;
            }

            var modes = new[] { AiReplyModes.Auto, AiReplyModes.Options, AiReplyModes.Suggestion };
            var modeLabels = new[]
            {
                T("ai.rule.mode.auto"),
                T("ai.rule.mode.options"),
                T("ai.rule.mode.suggestion"),
            };
            var modeIndex = Math.Max(0, Array.IndexOf(modes, rule.Mode));
            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            if (ImGui.Combo(T("ai.rule.mode"), ref modeIndex, modeLabels, modeLabels.Length))
                rule.Mode = modes[modeIndex];

            if (rule.Mode == AiReplyModes.Options)
            {
                var optionsCount = rule.OptionsCount;
                ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt(T("ai.rule.optionsCount"), ref optionsCount))
                    rule.OptionsCount = Math.Clamp(optionsCount, 1, 10);
            }

            var cooldown = rule.CooldownSeconds;
            ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt(T("ai.rule.cooldown"), ref cooldown))
                rule.CooldownSeconds = Math.Clamp(cooldown, 0, 3600);

            var perMinute = rule.MaxPerMinute;
            ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt(T("ai.rule.maxPerMinute"), ref perMinute))
                rule.MaxPerMinute = Math.Clamp(perMinute, 1, 120);

            ImGui.TextColored(ColorMuted, T("ai.rule.channels"));
            foreach (var (_, types) in AiChannels.TriggerGroups)
            {
                var labelKey = AiChannels.GetLabelKey(types[0]);
                var label = UiStrings.T(_plugin.Config.UiLanguage, labelKey);
                var selected = types.All(rule.ChannelTypes.Contains);
                if (ImGui.Checkbox($"{label}##{rule.Id}-{types[0]}", ref selected))
                {
                    if (selected)
                    {
                        foreach (var type in types)
                        {
                            if (!rule.ChannelTypes.Contains(type))
                                rule.ChannelTypes.Add(type);
                        }
                    }
                    else
                    {
                        rule.ChannelTypes.RemoveAll(types.Contains);
                    }
                }
            }
        }

        if (ImGui.Button(T("ai.rule.add")))
        {
            _editRules.Add(new AiTriggerRule
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"Rule {_editRules.Count + 1}",
                Enabled = false,
                AgentId = _editAgents.FirstOrDefault()?.Id ?? "",
                Mode = AiReplyModes.Auto,
                CooldownSeconds = 10,
                MaxPerMinute = 6,
                OptionsCount = 3,
            });
            _selectedRule = _editRules.Count - 1;
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(_editRules.Count == 0))
        {
            if (ImGui.Button(T("ai.rule.remove")))
            {
                _editRules.RemoveAt(_selectedRule);
                _selectedRule = Math.Clamp(_selectedRule, 0, Math.Max(0, _editRules.Count - 1));
            }
        }
    }

    // ── Draft / save ────────────────────────────────────────────────

    private void SyncFromConfig()
    {
        var config = _plugin.Config;
        _editEnabled = config.AiEnabled;
        _editBaseUrl = config.AiApiBaseUrl;
        _editApiKey = "";
        _editModel = config.AiDefaultModel;
        _editContextCount = config.AiContextMessageCount;
        _editTimeout = config.AiRequestTimeoutSeconds;
        _editLanguage = config.UiLanguage == "en" ? "en" : "zh";
        _editAgents = config.AiAgents.Select(CloneAgent).ToList();
        _editRules = config.AiRules.Select(CloneRule).ToList();
        _selectedAgent = 0;
        _selectedRule = 0;
        _testMessage = null;
        _testOk = false;
    }

    private void Save()
    {
        var config = _plugin.Config;
        config.AiEnabled = _editEnabled;
        config.AiApiBaseUrl = _editBaseUrl.Trim();
        config.AiDefaultModel = _editModel.Trim();
        config.AiContextMessageCount = Math.Clamp(_editContextCount, 1, 100);
        config.AiRequestTimeoutSeconds = Math.Clamp(_editTimeout, 5, 120);
        config.UiLanguage = _editLanguage == "en" ? "en" : "zh";

        if (!string.IsNullOrWhiteSpace(_editApiKey))
            config.AiApiKey = _editApiKey.Trim();

        config.AiAgents = _editAgents.Select(CloneAgent).ToList();
        config.AiRules = _editRules.Select(CloneRule).ToList();

        _plugin.SaveConfig();
        _plugin.Server.NotifyAiSettingsChanged();
        SyncFromConfig();
    }

    private static AiAgentConfig CloneAgent(AiAgentConfig source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        SystemPrompt = source.SystemPrompt,
        Model = source.Model,
        Temperature = source.Temperature,
        MaxTokens = source.MaxTokens,
    };

    private static AiTriggerRule CloneRule(AiTriggerRule source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Enabled = source.Enabled,
        AgentId = source.AgentId,
        Mode = source.Mode,
        ChannelTypes = [.. source.ChannelTypes],
        CooldownSeconds = source.CooldownSeconds,
        MaxPerMinute = source.MaxPerMinute,
        OptionsCount = source.OptionsCount,
    };

    private void StartTest()
    {
        if (_testInProgress)
            return;

        _testInProgress = true;
        _testMessage = null;

        _ = Task.Run(async () =>
        {
            var result = await _plugin.Ai.TestConnectionAsync(CancellationToken.None).ConfigureAwait(false);
            _testOk = result.Ok;
            _testMessage = result.Ok ? $"{result.LatencyMs} ms" : (result.Error ?? "error");
            _testInProgress = false;
        });
    }
}
