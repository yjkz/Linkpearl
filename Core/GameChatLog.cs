using System.Text;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Linkpearl.Core;

/// <summary>
/// Reads the game's own stored chat log. Dalamud only delivers live messages,
/// so this recovers lines that were printed before the plugin (re)loaded.
/// </summary>
internal static unsafe class GameChatLog
{
    /// <summary>
    /// Reads the newest <paramref name="maxMessages"/> stored chat lines,
    /// oldest first. Returns false when the game log module is not available
    /// yet (callers should retry later).
    /// </summary>
    public static bool TryReadRecent(
        int maxMessages,
        string localPlayerName,
        string localPlayerWorld,
        IPluginLog log,
        out List<ReceivedChatMessage> messages)
    {
        messages = [];
        try
        {
            var module = RaptureLogModule.Instance();
            if (module == null)
                return false;

            var stored = module->LogModule.LogMessageCount;
            if (stored <= 0)
                return true;

            var take = Math.Clamp(maxMessages, 1, 2000);
            var start = Math.Max(0, stored - take);

            for (var index = start; index < stored; index++)
            {
                if (!module->GetLogMessageDetail(
                        index,
                        out var senderBytes,
                        out var messageBytes,
                        out var logKind,
                        out _,
                        out _,
                        out var timestamp))
                {
                    break;
                }

                if (messageBytes.Length == 0)
                    continue;

                var message = SeString.Parse(messageBytes);
                var text = ExtractText(message);
                if (text.Length == 0)
                    continue;

                var sender = senderBytes.Length > 0 ? SeString.Parse(senderBytes) : null;
                var senderName = CleanName(sender?.TextValue ?? "");
                var senderWorld = "";
                if (sender != null)
                {
                    foreach (var payload in sender.Payloads)
                    {
                        if (payload is PlayerPayload player)
                        {
                            senderName = CleanName(player.PlayerName);
                            senderWorld = player.World.ValueNullable?.Name.ToString() ?? "";
                            break;
                        }
                    }
                }

                var type = (XivChatType)logKind;
                string? recipientName = null;
                string? recipientWorld = null;

                // For outgoing tells the game stores the recipient in the
                // sender column; mirror ChatReceiver's live handling.
                if (type == XivChatType.TellOutgoing && !string.IsNullOrEmpty(localPlayerName))
                {
                    recipientName = senderName;
                    recipientWorld = senderWorld;
                    senderName = localPlayerName;
                    senderWorld = localPlayerWorld;
                }

                messages.Add(new ReceivedChatMessage
                {
                    Type = (ushort)type,
                    SenderName = senderName,
                    SenderWorld = senderWorld,
                    RecipientName = recipientName,
                    RecipientWorld = recipientWorld,
                    MessagePayloads = new List<ChatPayloadDto>
                    {
                        new() { Type = "text", Text = text },
                    },
                    Timestamp = timestamp > 0
                        ? timestamp * 1000L
                        : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[AI] Failed to read the game chat log.");
            return true;
        }
    }

    private static string ExtractText(SeString message)
    {
        var builder = new StringBuilder();
        foreach (var payload in message.Payloads)
        {
            switch (payload)
            {
                case TextPayload text when !string.IsNullOrEmpty(text.Text):
                    builder.Append(text.Text);
                    break;

                case AutoTranslatePayload autoTranslate:
                {
                    // Strip the leading/trailing bracket glyphs.
                    var value = autoTranslate.Text;
                    if (value.Length >= 4)
                        value = value.Substring(2, value.Length - 4);
                    builder.Append(value.Trim());
                    break;
                }
            }
        }

        return builder.ToString().Trim();
    }

    private static string CleanName(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, @"[\uE000-\uF8FF]", "").Trim();
}
