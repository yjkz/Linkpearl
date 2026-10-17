using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin.Services;
using WatsonWebserver.Core;

namespace Linkpearl.Core;

/// <summary>REST surface for the AI assistant, shared by the browser UI.</summary>
internal class AiHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Plugin _plugin;
    private readonly IPluginLog _log;
    private readonly AuthHandler _auth;
    private readonly Ai.AiService _ai;
    private readonly Action _onSettingsChanged;

    internal AiHandler(Plugin plugin, IPluginLog log, AuthHandler auth, Ai.AiService ai, Action onSettingsChanged)
    {
        _plugin = plugin;
        _log = log;
        _auth = auth;
        _ai = ai;
        _onSettingsChanged = onSettingsChanged;
    }

    internal async Task HandleGetSettings(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        var config = _plugin.Config;
        var response = new
        {
            enabled = config.AiEnabled,
            apiKeySet = !string.IsNullOrEmpty(config.AiApiKey),
            apiBaseUrl = config.AiApiBaseUrl,
            defaultModel = config.AiDefaultModel,
            contextMessageCount = config.AiContextMessageCount,
            timeoutSeconds = config.AiRequestTimeoutSeconds,
            agents = config.AiAgents.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                systemPrompt = a.SystemPrompt,
                model = a.Model,
                temperature = a.Temperature,
                maxTokens = a.MaxTokens,
            }),
            rules = config.AiRules.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                enabled = r.Enabled,
                agentId = r.AgentId,
                mode = r.Mode,
                channelTypes = r.ChannelTypes,
                cooldownSeconds = r.CooldownSeconds,
                maxPerMinute = r.MaxPerMinute,
                optionsCount = r.OptionsCount,
            }),
        };

        await SendJson(ctx, JsonSerializer.Serialize(response, JsonOptions));
    }

    internal async Task HandlePutSettings(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        try
        {
            var body = await HttpHelper.ReadBodyAsync(ctx, 262_144);
            if (body == null)
            {
                await SendError(ctx, 413, "Request body too large");
                return;
            }

            var payload = JsonSerializer.Deserialize<AiSettingsPayload>(body, HttpHelper.CaseInsensitiveOptions);
            if (payload == null)
            {
                await SendError(ctx, 400, "Invalid JSON");
                return;
            }

            ApplyPayload(payload);
            _plugin.SaveConfig();
            _onSettingsChanged();

            await SendJson(ctx, "{\"ok\":true}");
        }
        catch (JsonException)
        {
            await SendError(ctx, 400, "Invalid JSON");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling PUT /ai/settings");
            await SendError(ctx, 500, "Internal Server Error");
        }
    }

    private void ApplyPayload(AiSettingsPayload payload)
    {
        var config = _plugin.Config;
        if (payload.Enabled is { } enabled) config.AiEnabled = enabled;
        if (payload.ApiBaseUrl != null) config.AiApiBaseUrl = payload.ApiBaseUrl.Trim();
        if (payload.DefaultModel != null) config.AiDefaultModel = payload.DefaultModel.Trim();
        if (payload.ContextMessageCount is { } context)
            config.AiContextMessageCount = Math.Clamp(context, 1, 100);
        if (payload.TimeoutSeconds is { } timeout)
            config.AiRequestTimeoutSeconds = Math.Clamp(timeout, 5, 120);

        if (payload.ClearApiKey == true)
            config.AiApiKey = "";
        else if (!string.IsNullOrWhiteSpace(payload.ApiKey))
            config.AiApiKey = payload.ApiKey.Trim();

        if (payload.Agents != null)
        {
            config.AiAgents = payload.Agents.Select(a => new Ai.AiAgentConfig
            {
                Id = string.IsNullOrWhiteSpace(a.Id) ? Guid.NewGuid().ToString("N") : a.Id!,
                Name = string.IsNullOrWhiteSpace(a.Name) ? "Assistant" : a.Name!.Trim(),
                SystemPrompt = a.SystemPrompt ?? "",
                Model = a.Model?.Trim() ?? "",
                Temperature = Math.Clamp(a.Temperature ?? 0.7, 0.0, 2.0),
                MaxTokens = Math.Clamp(a.MaxTokens ?? 300, 1, 4096),
            }).ToList();
        }

        if (config.AiAgents.Count == 0)
        {
            config.AiAgents.Add(new Ai.AiAgentConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "Assistant",
                SystemPrompt = "",
                Temperature = 0.7,
                MaxTokens = 300,
            });
        }

        var defaultAgentId = config.AiAgents[0].Id;
        if (payload.Rules != null)
        {
            config.AiRules = payload.Rules.Select(r => new Ai.AiTriggerRule
            {
                Id = string.IsNullOrWhiteSpace(r.Id) ? Guid.NewGuid().ToString("N") : r.Id!,
                Name = string.IsNullOrWhiteSpace(r.Name) ? "Rule" : r.Name!.Trim(),
                Enabled = r.Enabled ?? true,
                AgentId = config.AiAgents.Any(a => a.Id == r.AgentId) ? r.AgentId! : defaultAgentId,
                Mode = (r.Mode is Ai.AiReplyModes.Auto or Ai.AiReplyModes.Options or Ai.AiReplyModes.Suggestion)
                    ? r.Mode!
                    : Ai.AiReplyModes.Auto,
                ChannelTypes = (r.ChannelTypes ?? new List<ushort>())
                    .Where(Ai.AiChannels.IsChatType)
                    .Distinct()
                    .ToList(),
                CooldownSeconds = Math.Clamp(r.CooldownSeconds ?? 10, 0, 3600),
                MaxPerMinute = Math.Clamp(r.MaxPerMinute ?? 6, 1, 120),
                OptionsCount = Math.Clamp(r.OptionsCount ?? 3, 1, 10),
            }).ToList();
        }
    }

    internal async Task HandleGetSuggestions(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        var response = new { suggestions = _ai.GetSuggestions() };
        await SendJson(ctx, JsonSerializer.Serialize(response, JsonOptions));
    }

    internal async Task HandleGetActivity(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        var response = new { activity = _ai.GetActivity() };
        await SendJson(ctx, JsonSerializer.Serialize(response, JsonOptions));
    }

    internal async Task HandleSendSuggestion(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        try
        {
            var body = await HttpHelper.ReadBodyAsync(ctx, 8192);
            if (body == null)
            {
                await SendError(ctx, 413, "Request body too large");
                return;
            }

            var payload = JsonSerializer.Deserialize<SendSuggestionPayload>(body, HttpHelper.CaseInsensitiveOptions);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Id))
            {
                await SendError(ctx, 400, "Invalid request");
                return;
            }

            if (_ai.TrySendSuggestion(payload.Id, payload.Index, payload.Text, out var error))
                await SendJson(ctx, "{\"ok\":true}");
            else
                await SendError(ctx, 404, error);
        }
        catch (JsonException)
        {
            await SendError(ctx, 400, "Invalid JSON");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling POST /ai/suggestions/send");
            await SendError(ctx, 500, "Internal Server Error");
        }
    }

    internal async Task HandleDismissSuggestion(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        try
        {
            var body = await HttpHelper.ReadBodyAsync(ctx, 4096);
            if (body == null)
            {
                await SendError(ctx, 413, "Request body too large");
                return;
            }

            var payload = JsonSerializer.Deserialize<DismissSuggestionPayload>(body, HttpHelper.CaseInsensitiveOptions);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Id))
            {
                await SendError(ctx, 400, "Invalid request");
                return;
            }

            _ai.DismissSuggestion(payload.Id);
            await SendJson(ctx, "{\"ok\":true}");
        }
        catch (JsonException)
        {
            await SendError(ctx, 400, "Invalid JSON");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling POST /ai/suggestions/dismiss");
            await SendError(ctx, 500, "Internal Server Error");
        }
    }

    internal async Task HandleChat(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        try
        {
            var body = await HttpHelper.ReadBodyAsync(ctx, 131_072);
            if (body == null)
            {
                await SendError(ctx, 413, "Request body too large");
                return;
            }

            var payload = JsonSerializer.Deserialize<AiChatPayload>(body, HttpHelper.CaseInsensitiveOptions);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Message))
            {
                await SendError(ctx, 400, "Message is required");
                return;
            }

            var reply = await _ai.ChatAsync(
                payload.AgentId,
                payload.Message,
                payload.ContextScopes ?? new List<string>(),
                payload.Conversation ?? new List<Ai.AiChatTurn>(),
                CancellationToken.None);

            await SendJson(ctx, JsonSerializer.Serialize(new { reply }, JsonOptions));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling POST /ai/chat");
            await SendError(ctx, 500, ex.Message);
        }
    }

    internal async Task HandleTest(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        try
        {
            var result = await _ai.TestConnectionAsync(CancellationToken.None);
            var response = new
            {
                ok = result.Ok,
                latencyMs = result.Ok ? result.LatencyMs : 0,
                model = result.Model,
                sample = result.Sample,
                error = result.Error,
            };
            await SendJson(ctx, JsonSerializer.Serialize(response, JsonOptions), result.Ok ? 200 : 400);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling POST /ai/test");
            await SendError(ctx, 500, ex.Message);
        }
    }

    private async Task SendJson(HttpContextBase ctx, string json, int statusCode = 200)
    {
        ctx.Response.StatusCode = statusCode;
        _auth.AddCorsHeaders(ctx);
        ctx.Response.Headers.Add("Content-Type", "application/json");
        await ctx.Response.Send(json);
    }

    private async Task SendError(HttpContextBase ctx, int statusCode, string error)
    {
        await SendJson(ctx, JsonSerializer.Serialize(new { error }), statusCode);
    }
}

internal sealed class AiSettingsPayload
{
    public bool? Enabled { get; set; }
    public string? ApiBaseUrl { get; set; }
    public string? DefaultModel { get; set; }
    public int? ContextMessageCount { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? ApiKey { get; set; }
    public bool? ClearApiKey { get; set; }
    public List<AiAgentPayload>? Agents { get; set; }
    public List<AiRulePayload>? Rules { get; set; }
}

internal sealed class AiAgentPayload
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
}

internal sealed class AiRulePayload
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
    public string? AgentId { get; set; }
    public string? Mode { get; set; }
    public List<ushort>? ChannelTypes { get; set; }
    public int? CooldownSeconds { get; set; }
    public int? MaxPerMinute { get; set; }
    public int? OptionsCount { get; set; }
}

internal sealed class SendSuggestionPayload
{
    public string Id { get; set; } = "";
    public int? Index { get; set; }
    public string? Text { get; set; }
}

internal sealed class AiChatPayload
{
    public string? AgentId { get; set; }
    public string Message { get; set; } = "";
    public List<string>? ContextScopes { get; set; }
    public List<Ai.AiChatTurn>? Conversation { get; set; }
}

internal sealed class DismissSuggestionPayload
{
    public string Id { get; set; } = "";
}
