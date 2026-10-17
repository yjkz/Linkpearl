using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Linkpearl.Core;
using Linkpearl.Core.Ai;
using Linkpearl.Ui;

namespace Linkpearl;

public sealed class Plugin : IAsyncDalamudPlugin
{
    public string Name => "Linkpearl";

    public IDalamudPluginInterface Interface { get; init; }
    public IPluginLog Log { get; init; }
    public IChatGui Chat { get; init; }
    public IClientState ClientState { get; init; }
    public IObjectTable ObjectTable { get; init; }
    public IFramework Framework { get; init; }
    public IDataManager DataManager { get; init; }
    public IGameConfig GameConfig { get; init; }
    public ICommandManager CommandManager { get; init; }

    public Configuration Config { get; init; }

    public ChatReceiver Receiver { get; }
    public ChatSender Sender { get; init; }
    public FontManager FontManager { get; }
    public WebServer Server { get; init; }
    public AiService Ai { get; }

    public WindowSystem WindowSystem { get; } = new("Linkpearl");
    public SettingsWindow SettingsWindow { get; }
    public AiWindow AiWindow { get; }

    private const string CommandName = "/linkpearl";
    private string _lastGameChannelPrefix = string.Empty;
    private DateTimeOffset _lastChannelPollTime = DateTimeOffset.MinValue;
    /// <summary>
    /// The local player's name. Set by OnFrameworkUpdate once LocalPlayer is confirmed,
    /// cleared by OnLogin. Read by WebServer from the SSE handler thread — must be volatile.
    /// </summary>
    public volatile string LocalPlayerName = string.Empty;

    /// <summary>
    /// The local player's home world. Set and cleared alongside LocalPlayerName.
    /// Read by WebServer from the SSE handler thread — must be volatile.
    /// </summary>
    public volatile string LocalPlayerWorld = string.Empty;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IChatGui chat,
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDataManager dataManager,
        IGameConfig gameConfig,
        ICommandManager commandManager)
    {
        Interface      = pluginInterface;
        Log            = log;
        Chat           = chat;
        ClientState    = clientState;
        ObjectTable    = objectTable;
        Framework      = framework;
        DataManager    = dataManager;
        GameConfig     = gameConfig;
        CommandManager = commandManager;

        Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();

        Sender      = new ChatSender(Log);
        Receiver    = new ChatReceiver(Chat, ClientState, ObjectTable, Log);
        FontManager = new FontManager(Interface, DataManager, Log);
        Ai          = new AiService(this, Sender, Receiver, Log);
        Server      = new WebServer(this, Sender, Receiver, Log);

        SettingsWindow = new SettingsWindow(this);
        AiWindow       = new AiWindow(this);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(AiWindow);
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Ai.EnsureDefaults())
            SaveConfig();

        if (Config.WebinterfaceEnabled)
            Server.Start();

        // Build the emote list cache on the framework thread (UIState access required).
        Framework.RunOnTick(() => Server.RefreshEmoteList());

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = UiStrings.T(Config.UiLanguage, "command.help"),
        });

        Framework.Update   += OnFrameworkUpdate;
        ClientState.Login  += OnLogin;
        ClientState.Logout += OnLogout;
        Interface.UiBuilder.Draw         += WindowSystem.Draw;
        Interface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        Interface.UiBuilder.OpenMainUi   += OnOpenConfigUi;

        return Task.CompletedTask;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "start":
                if (!Server.IsRunning)
                {
                    Server.Start();
                    Chat.Print(UiStrings.T(Config.UiLanguage, "command.serverStarted"));
                }
                else
                {
                    Chat.Print(UiStrings.T(Config.UiLanguage, "command.serverRunning"));
                }
                break;

            case "stop":
                if (Server.IsRunning)
                {
                    Server.Stop();
                    Chat.Print(UiStrings.T(Config.UiLanguage, "command.serverStopped"));
                }
                else
                {
                    Chat.Print(UiStrings.T(Config.UiLanguage, "command.serverNotRunning"));
                }
                break;

            case "ai":
                AiWindow.IsOpen = !AiWindow.IsOpen;
                break;

            case "help":
                Chat.Print(UiStrings.T(Config.UiLanguage, "command.helpHeader"));
                Chat.Print(UiStrings.T(Config.UiLanguage, "command.helpToggle"));
                Chat.Print(UiStrings.T(Config.UiLanguage, "command.helpStart"));
                Chat.Print(UiStrings.T(Config.UiLanguage, "command.helpStop"));
                Chat.Print(UiStrings.T(Config.UiLanguage, "command.helpAi"));
                break;

            default:
                SettingsWindow.IsOpen = !SettingsWindow.IsOpen;
                break;
        }
    }

    private void OnOpenConfigUi()
    {
        SettingsWindow.IsOpen = true;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Server.IsRunning) return;

        // Sync active channel prefix on every frame (cheap string compare)
        var currentPrefix = Server.GetCurrentGameChannelPrefix();
        if (currentPrefix != _lastGameChannelPrefix)
        {
            _lastGameChannelPrefix = currentPrefix;
            if (!string.IsNullOrEmpty(currentPrefix))
                Server.SendActiveChannel(currentPrefix);
        }

        // Poll channel list once per second; broadcast only when it has changed
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastChannelPollTime).TotalSeconds >= 1.0)
        {
            _lastChannelPollTime = now;
            Server.PollAndBroadcastChannels();
        }

        // Once LocalPlayer is confirmed and name is not yet stored, capture and broadcast it.
        // Covers both the post-login case and mid-session plugin startup.
        // Runs every frame until LocalPlayerName is set, then becomes a no-op.
        if (string.IsNullOrEmpty(LocalPlayerName) && ObjectTable.LocalPlayer != null)
        {
            LocalPlayerName = ObjectTable.LocalPlayer.Name.TextValue;
            LocalPlayerWorld = ObjectTable.LocalPlayer.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;
            Server.BroadcastPlayerInfo(LocalPlayerName, LocalPlayerWorld);
        }
    }

    private void OnLogout(int type, int code)
    {
        LocalPlayerName = string.Empty;
        LocalPlayerWorld = string.Empty;
        Ai.HandleLogout();
        if (Server.IsRunning)
            Server.BroadcastPlayerInfo(string.Empty, string.Empty);
    }

    private void OnLogin()
    {
        if (!Server.IsRunning) return;
        LocalPlayerName = string.Empty;
        LocalPlayerWorld = string.Empty;
        Server.BroadcastReset();
        Server.RefreshEmoteList();
        // LocalPlayerName being empty re-arms the OnFrameworkUpdate check,
        // which will broadcast player info once LocalPlayer is confirmed.
    }

    public void SaveConfig()
    {
        Interface.SavePluginConfig(Config);
    }

    public async ValueTask DisposeAsync()
    {
        CommandManager.RemoveHandler(CommandName);

        Framework.Update   -= OnFrameworkUpdate;
        ClientState.Login  -= OnLogin;
        ClientState.Logout -= OnLogout;
        Interface.UiBuilder.Draw         -= WindowSystem.Draw;
        Interface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        Interface.UiBuilder.OpenMainUi   -= OnOpenConfigUi;

        WindowSystem.RemoveAllWindows();

        Ai.Dispose();
        await Server.DisposeAsync().ConfigureAwait(false);
        Receiver.Dispose();

        SaveConfig();
    }
}
