namespace Linkpearl.Core.Ai;

/// <summary>Reply mode identifiers stored as strings in the plugin configuration.</summary>
public static class AiReplyModes
{
    public const string Auto       = "auto";
    public const string Options    = "options";
    public const string Suggestion = "suggestion";
}

/// <summary>
/// A customizable agent profile. The system prompt defines its behaviour;
/// the model/temperature/max-token values override the global defaults.
/// </summary>
[Serializable]
public sealed class AiAgentConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string Model { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 300;
}

/// <summary>
/// A channel trigger rule. Every new message whose type is listed in
/// <see cref="ChannelTypes"/> is sent to the bound agent.
/// </summary>
[Serializable]
public sealed class AiTriggerRule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string AgentId { get; set; } = "";
    public string Mode { get; set; } = AiReplyModes.Auto;
    public List<ushort> ChannelTypes { get; set; } = new();
    public int CooldownSeconds { get; set; } = 10;
    public int MaxPerMinute { get; set; } = 6;
    public int OptionsCount { get; set; } = 3;
}

/// <summary>One message in the OpenAI chat-completions request.</summary>
public sealed class AiChatMessage
{
    public string Role { get; init; } = "user";
    public string Content { get; init; } = "";
}

/// <summary>One turn in the manual AI conversation UI (sent by the browser or ImGui).</summary>
public sealed class AiChatTurn
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

/// <summary>A manual-chat reply plus the number of game messages attached as context.</summary>
public sealed class AiChatResult
{
    public string Reply { get; init; } = "";
    public int ContextMessageCount { get; init; }
}

/// <summary>
/// A generated suggestion set waiting for the user to pick/send/edit.
/// Serialized to the browser as-is; <see cref="ReplyPrefix"/> stays server-side.
/// </summary>
public sealed class AiSuggestionSet
{
    public string Id { get; init; } = "";
    public string RuleId { get; init; } = "";
    public string RuleName { get; init; } = "";
    public string AgentName { get; init; } = "";
    public string Mode { get; init; } = AiReplyModes.Options;
    public ushort ChannelType { get; init; }
    public string ChannelLabel { get; init; } = "";
    public string SenderName { get; init; } = "";
    public string SenderWorld { get; init; } = "";
    public string SourceText { get; init; } = "";
    public List<string> Candidates { get; init; } = new();
    public long CreatedAt { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string ReplyPrefix { get; init; } = "";
}

/// <summary>An entry in the AI activity log (auto replies, errors, info).</summary>
public sealed class AiActivityEntry
{
    public long Timestamp { get; init; }
    public string Kind { get; init; } = "info"; // info | auto-reply | error
    public string Message { get; init; } = "";
}
