using System.Text.Json;
using Dalamud.Plugin.Services;
using WatsonWebserver.Core;

namespace Linkpearl.Core;

/// <summary>
/// Reads/writes the shared UI language (Configuration.UiLanguage) for the browser.
/// The browser and the in-game windows therefore always show the same language.
/// </summary>
internal class LanguageHandler
{
    private readonly Plugin _plugin;
    private readonly IPluginLog _log;
    private readonly AuthHandler _auth;
    private readonly Action<string> _onChanged;

    internal LanguageHandler(Plugin plugin, IPluginLog log, AuthHandler auth, Action<string> onChanged)
    {
        _plugin = plugin;
        _log = log;
        _auth = auth;
        _onChanged = onChanged;
    }

    internal async Task HandleGetLanguage(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        ctx.Response.StatusCode = 200;
        _auth.AddCorsHeaders(ctx);
        ctx.Response.Headers.Add("Content-Type", "application/json");
        await ctx.Response.Send(JsonSerializer.Serialize(new { language = Normalize(_plugin.Config.UiLanguage) }));
    }

    internal async Task HandlePutLanguage(HttpContextBase ctx)
    {
        if (!await _auth.RequireAuth(ctx)) return;

        try
        {
            var body = await HttpHelper.ReadBodyAsync(ctx, 1024);
            if (body == null)
            {
                await SendBadRequest(ctx);
                return;
            }

            var payload = string.IsNullOrEmpty(body)
                ? null
                : JsonSerializer.Deserialize<LanguagePayload>(body, HttpHelper.CaseInsensitiveOptions);

            var requested = payload?.Language;
            if (!IsValid(requested))
            {
                await SendBadRequest(ctx);
                return;
            }

            var language = Normalize(requested);
            _plugin.Config.UiLanguage = language;
            _plugin.SaveConfig();
            _onChanged(language);

            ctx.Response.StatusCode = 200;
            _auth.AddCorsHeaders(ctx);
            ctx.Response.Headers.Add("Content-Type", "application/json");
            await ctx.Response.Send(JsonSerializer.Serialize(new { ok = true, language }));
        }
        catch (JsonException)
        {
            await SendBadRequest(ctx);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling PUT /language");
            ctx.Response.StatusCode = 500;
            _auth.AddCorsHeaders(ctx);
            await ctx.Response.Send("Internal Server Error");
        }
    }

    private async Task SendBadRequest(HttpContextBase ctx)
    {
        ctx.Response.StatusCode = 400;
        _auth.AddCorsHeaders(ctx);
        await ctx.Response.Send("{\"error\":\"invalid-language\"}");
    }

    private static bool IsValid(string? language) =>
        string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase)
        || string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
}

internal sealed class LanguagePayload
{
    public string? Language { get; set; }
}
