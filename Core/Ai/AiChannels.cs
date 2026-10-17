using Dalamud.Game.Text;

namespace Linkpearl.Core.Ai;

/// <summary>
/// Channel metadata shared by the AI service and both UIs.
/// Only real player-chat channels are eligible for AI triggers.
/// </summary>
public static class AiChannels
{
    /// <summary>Groups shown in the rule editor. A group toggles all of its chat types.</summary>
    public static readonly (string Label, ushort[] Types)[] TriggerGroups =
    [
        ("Say",           [10]),
        ("Party",         [14, 32]),
        ("Alliance",      [15]),
        ("Yell",          [30]),
        ("Shout",         [11]),
        ("FreeCompany",   [24]),
        ("NoviceNetwork", [27]),
        ("Tell",          [13]),
        ("LS1",           [16]),
        ("LS2",           [17]),
        ("LS3",           [18]),
        ("LS4",           [19]),
        ("LS5",           [20]),
        ("LS6",           [21]),
        ("LS7",           [22]),
        ("LS8",           [23]),
        ("CWLS1",         [37]),
        ("CWLS2",         [101]),
        ("CWLS3",         [102]),
        ("CWLS4",         [103]),
        ("CWLS5",         [104]),
        ("CWLS6",         [105]),
        ("CWLS7",         [106]),
        ("CWLS8",         [107]),
    ];

    private static readonly HashSet<ushort> ChatTypes = BuildChatTypeSet();

    private static HashSet<ushort> BuildChatTypeSet()
    {
        var set = new HashSet<ushort>();
        foreach (var (_, types) in TriggerGroups)
        {
            foreach (var type in types)
                set.Add(type);
        }
        return set;
    }

    public static bool IsChatType(ushort type) => ChatTypes.Contains(type);

    /// <summary>Canonical English label used in API payloads and activity messages.</summary>
    public static string GetLabel(ushort type) => type switch
    {
        10 => "Say",
        11 => "Shout",
        12 => "Tell",
        13 => "Tell",
        14 => "Party",
        15 => "Alliance",
        16 => "LS1",
        17 => "LS2",
        18 => "LS3",
        19 => "LS4",
        20 => "LS5",
        21 => "LS6",
        22 => "LS7",
        23 => "LS8",
        24 => "FreeCompany",
        27 => "NoviceNetwork",
        30 => "Yell",
        32 => "Party",
        36 => "PvPTeam",
        37 => "CWLS1",
        101 => "CWLS2",
        102 => "CWLS3",
        103 => "CWLS4",
        104 => "CWLS5",
        105 => "CWLS6",
        106 => "CWLS7",
        107 => "CWLS8",
        _ => "Chat",
    };

    /// <summary>Chat command prefix used when replying to a channel. Empty when unsupported.</summary>
    public static string GetReplyPrefix(ushort type) => type switch
    {
        10 => "/s ",
        11 => "/sh ",
        14 => "/p ",
        15 => "/a ",
        24 => "/fc ",
        27 => "/n ",
        30 => "/y ",
        32 => "/p ",
        16 => "/l1 ",
        17 => "/l2 ",
        18 => "/l3 ",
        19 => "/l4 ",
        20 => "/l5 ",
        21 => "/l6 ",
        22 => "/l7 ",
        23 => "/l8 ",
        37 => "/cwl1 ",
        101 => "/cwl2 ",
        102 => "/cwl3 ",
        103 => "/cwl4 ",
        104 => "/cwl5 ",
        105 => "/cwl6 ",
        106 => "/cwl7 ",
        107 => "/cwl8 ",
        _ => "",
    };

    /// <summary>Localization key for the channel label.</summary>
    public static string GetLabelKey(ushort type) => type switch
    {
        10 => "channel.say",
        11 => "channel.shout",
        12 => "channel.tell",
        13 => "channel.tell",
        14 => "channel.party",
        15 => "channel.alliance",
        16 => "channel.ls1",
        17 => "channel.ls2",
        18 => "channel.ls3",
        19 => "channel.ls4",
        20 => "channel.ls5",
        21 => "channel.ls6",
        22 => "channel.ls7",
        23 => "channel.ls8",
        24 => "channel.freeCompany",
        27 => "channel.noviceNetwork",
        30 => "channel.yell",
        32 => "channel.party",
        36 => "channel.pvpTeam",
        37 => "channel.cwls1",
        101 => "channel.cwls2",
        102 => "channel.cwls3",
        103 => "channel.cwls4",
        104 => "channel.cwls5",
        105 => "channel.cwls6",
        106 => "channel.cwls7",
        107 => "channel.cwls8",
        _ => "channel.chat",
    };
}
