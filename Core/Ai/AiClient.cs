using System.Net.Http;
using System.Text;
using System.Text.Json;
using Dalamud.Plugin.Services;

namespace Linkpearl.Core.Ai;

/// <summary>
/// Minimal OpenAI-compatible chat-completions client. No tool calling, no streaming.
/// </summary>
public sealed class AiClient : IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan,
    };

    private readonly IPluginLog _log;

    public AiClient(IPluginLog log)
    {
        _log = log;
    }

    /// <summary>
    /// Sends a chat-completions request and returns the assistant message content.
    /// </summary>
    public async Task<string> CompleteAsync(
        string baseUrl,
        string apiKey,
        string model,
        IReadOnlyList<AiChatMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("API Base URL is not configured.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("API Key is not configured.");
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Model is not configured.");

        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages
                .Select(m => new Dictionary<string, string> { ["role"] = m.Role, ["content"] = m.Content })
                .ToArray(),
            ["temperature"] = Math.Clamp(temperature, 0.0, 2.0),
            ["max_tokens"] = Math.Clamp(maxTokens, 1, 4096),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(baseUrl));
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var snippet = body.Length > 600 ? body[..600] : body;
            throw new InvalidOperationException($"API error {(int)response.StatusCode}: {snippet}");
        }

        return ExtractContent(body);
    }

    /// <summary>Validates connectivity and credentials with a minimal request.</summary>
    public async Task<AiTestResult> TestAsync(
        string baseUrl,
        string apiKey,
        string model,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120)));

        try
        {
            var reply = await CompleteAsync(
                baseUrl,
                apiKey,
                model,
                new[] { new AiChatMessage { Role = "user", Content = "Reply with the single word: OK" } },
                0.0,
                16,
                timeout.Token).ConfigureAwait(false);

            return new AiTestResult
            {
                Ok = true,
                LatencyMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
                Model = model,
                Sample = reply.Length > 120 ? reply[..120] : reply,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiTestResult { Ok = false, Error = "Request timed out." };
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "[AI] Connection test failed.");
            return new AiTestResult { Ok = false, Error = ex.Message };
        }
    }

    private static string BuildEndpoint(string baseUrl)
    {
        var url = baseUrl.Trim();
        if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return url;
        return url.TrimEnd('/') + "/chat/completions";
    }

    private static string ExtractContent(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("API response did not contain any choices.");
        }

        if (!choices[0].TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content))
        {
            throw new InvalidOperationException("API response did not contain a message.");
        }

        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? "";

        // Some compatible providers return an array of content parts.
        if (content.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object
                    && part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }
            return builder.ToString();
        }

        throw new InvalidOperationException("API response contained an unsupported message format.");
    }

    /// <summary>
    /// Parses a model response into up to <paramref name="count"/> candidate replies.
    /// Prefers a JSON string array, then falls back to one candidate per line.
    /// </summary>
    public static string[] ParseCandidates(string content, int count)
    {
        count = Math.Clamp(count, 1, 10);
        var text = StripCodeFence(content.Trim());

        var json = TryParseJsonArray(text);
        if (json is { Length: > 0 })
            return json.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Take(count).ToArray();

        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanLine)
            .Where(line => line.Length > 0)
            .Take(count)
            .ToArray();

        return lines.Length > 0 ? lines : [text];
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
            return text;

        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0) return text;
        var body = text[(firstNewline + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return closing >= 0 ? body[..closing].Trim() : body.Trim();
    }

    private static string[]? TryParseJsonArray(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
                return ReadStringArray(root);

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertyName in new[] { "options", "replies", "candidates", "responses" })
                {
                    if (root.TryGetProperty(propertyName, out var array)
                        && array.ValueKind == JsonValueKind.Array)
                    {
                        return ReadStringArray(array);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON — use the line-based fallback.
        }

        return null;
    }

    private static string[] ReadStringArray(JsonElement array)
    {
        var values = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                values.Add(item.GetString() ?? "");
        }
        return values.ToArray();
    }

    private static string CleanLine(string line)
    {
        var value = line.Trim();
        // Strip common list markers: "- ", "* ", "1. ", "1) ".
        value = System.Text.RegularExpressions.Regex.Replace(value, @"^[-*•]\s*", "");
        value = System.Text.RegularExpressions.Regex.Replace(value, @"^\d+[.)]\s*", "");
        // Strip a single layer of wrapping quotes.
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }
        return value.Trim();
    }

    public void Dispose()
    {
        // HttpClient is shared and intentionally not disposed per instance.
    }
}

public sealed class AiTestResult
{
    public bool Ok { get; init; }
    public long LatencyMs { get; init; }
    public string? Model { get; init; }
    public string? Sample { get; init; }
    public string? Error { get; init; }
}
