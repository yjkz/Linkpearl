using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace Linkpearl.Ui;

public sealed class SettingsWindow : Window
{
    private readonly Plugin _plugin;

    private int _editPort;
    private bool _editAutoStart;
    private string _editPassword = "";
    private string _editPasswordConfirm = "";
    private string _editLanguage = "zh";

    // Snapshot of saved values — used to detect unsaved changes
    private int _savedPort;
    private bool _savedAutoStart;
    private string _savedPasswordHash = "";
    private string _savedLanguage = "zh";

    // Cached once on open — network enumeration is too expensive to run every frame
    private List<(string Label, string Url)> _cachedUrls = [];

    private static readonly Vector4 ColorGreen  = new(0.4f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 ColorRed    = new(0.9f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 ColorYellow = new(1f, 0.85f, 0.2f, 1f);
    private static readonly Vector4 ColorMuted  = new(0.6f, 0.6f, 0.6f, 1f);

    public SettingsWindow(Plugin plugin)
        : base($"{UiStrings.T(plugin.Config.UiLanguage, "window.settingsTitle")}###linkpearl-settings",
               ImGuiWindowFlags.AlwaysAutoResize)
    {
        _plugin = plugin;
    }

    private string T(string key) => UiStrings.T(_plugin.Config.UiLanguage, key);

    private void UpdateWindowTitle()
    {
        var title = $"{T("window.settingsTitle")}###linkpearl-settings";
        if (WindowName != title)
            WindowName = title;
    }

    public override void OnOpen()
    {
        SyncFromConfig();
        RefreshUrls();
    }

    private void RefreshUrls()
    {
        var port = _plugin.Config.WebinterfacePort;
        var urls = new List<(string, string)>
        {
            ("localhost", $"http://localhost:{port}/"),
        };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = addr.Address.ToString();
                    if (ip == "127.0.0.1") continue;
                    urls.Add((nic.Name, $"http://{ip}:{port}/"));
                }
            }
        }
        catch { /* ignore NIC enumeration failure */ }

        _cachedUrls = urls;
    }

    private void SyncFromConfig()
    {
        _editPort             = _plugin.Config.WebinterfacePort;
        _editAutoStart        = _plugin.Config.WebinterfaceEnabled;
        _editLanguage         = _plugin.Config.UiLanguage == "en" ? "en" : "zh";
        _editPassword         = "";
        _editPasswordConfirm  = "";
        _savedPort            = _editPort;
        _savedAutoStart       = _editAutoStart;
        _savedPasswordHash    = _plugin.Config.WebinterfacePasswordHash;
        _savedLanguage        = _editLanguage;
    }

    /// <summary>
    /// Returns true if any edit field has been changed since the last save/open.
    /// </summary>
    private bool HasUnsavedChanges()
    {
        if (_editPort != _savedPort) return true;
        if (_editAutoStart != _savedAutoStart) return true;
        if (_editLanguage != _savedLanguage) return true;
        if (_editPassword.Length > 0) return true;
        return false;
    }

    /// <summary>
    /// Returns true if the password fields contain a new valid passcode ready to save.
    /// </summary>
    private bool IsNewPasscodeValid() =>
        _editPassword.Length >= 4
        && _editPassword.Length <= 8
        && _editPassword == _editPasswordConfirm;

    private void Save()
    {
        _plugin.Config.WebinterfacePort    = _editPort;
        _plugin.Config.WebinterfaceEnabled = _editAutoStart;
        _plugin.Config.UiLanguage          = _editLanguage;

        if (IsNewPasscodeValid())
            _plugin.Config.WebinterfacePasswordHash = Configuration.HashPasscode(_editPassword);

        _plugin.SaveConfig();
        _plugin.Server.NotifyUiLanguageChanged();

        // Update snapshot so unsaved-changes warning clears
        _editPassword        = "";
        _editPasswordConfirm = "";
        _savedPort           = _editPort;
        _savedAutoStart      = _editAutoStart;
        _savedPasswordHash   = _plugin.Config.WebinterfacePasswordHash;
        _savedLanguage       = _editLanguage;
    }

    public override void Draw()
    {
        UpdateWindowTitle();

        var server    = _plugin.Server;
        var isRunning = server.IsRunning;

        // ─── Language ──────────────────────────────────────────────────
        SectionHeader(T("section.language"));

        var languageLabels = new[] { T("language.zh"), T("language.en") };
        var languageIndex  = _editLanguage == "en" ? 1 : 0;
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##ui-language", ref languageIndex, languageLabels, languageLabels.Length))
        {
            _editLanguage = languageIndex == 1 ? "en" : "zh";
            _savedLanguage = _editLanguage;
            _plugin.Config.UiLanguage = _editLanguage;
            _plugin.SaveConfig();
            _plugin.Server.NotifyUiLanguageChanged();
        }

        // ─── Server Control ────────────────────────────────────────────
        SectionHeader(T("section.serverControl"));

        ImGui.TextColored(isRunning ? ColorGreen : ColorRed,
                          isRunning ? T("server.running") : T("server.stopped"));

        ImGui.SameLine(80 * ImGuiHelpers.GlobalScale);

        using (ImRaii.Disabled(isRunning))
        {
            if (ImGui.SmallButton(T("server.start")))
                _plugin.Server.Start();
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(!isRunning))
        {
            if (ImGui.SmallButton(T("server.stop")))
                _plugin.Server.Stop();
        }

        ImGui.Spacing();

        // Access URLs (IPv4)
        ImGui.TextColored(ColorMuted, T("server.accessUrls"));
        ImGui.SameLine();
        if (ImGui.SmallButton(T("common.refresh")))
            RefreshUrls();
        foreach (var (label, url) in _cachedUrls)
            DrawUrlRow(label, url);

        ImGui.Spacing();

        // ─── Settings ─────────────────────────────────────────────────
        SectionHeader(T("section.settings"));

        ImGui.Checkbox(T("settings.autoStart"), ref _editAutoStart);

        ImGui.Spacing();

        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(T("settings.port"), ref _editPort))
            _editPort = Math.Clamp(_editPort, 1024, 49151);

        ImGui.SameLine();
        ImGui.TextColored(ColorMuted, T("settings.portRange"));

        if (isRunning && server.ActivePort != _editPort)
            ImGui.TextColored(ColorYellow, T("settings.portChangeHint"));

        ImGui.Spacing();

        if (ImGui.Button(T("settings.openAi")))
            _plugin.AiWindow.IsOpen = true;

        ImGui.Spacing();

        // ─── Security ─────────────────────────────────────────────────
        SectionHeader(T("section.security"));

        // Show warning if no passcode has been saved yet
        if (string.IsNullOrEmpty(_savedPasswordHash))
        {
            ImGui.TextColored(ColorRed, T("security.passcodeMissing"));
            ImGui.Spacing();
        }

        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputText($"{T("security.newPasscode")}##passcode", ref _editPassword, 8, ImGuiInputTextFlags.Password);
        ImGui.SameLine();
        ImGui.TextColored(ColorMuted, T("security.passcodeHint"));

        var digitsOnly = new string(_editPassword.Where(char.IsDigit).ToArray());
        if (digitsOnly != _editPassword) _editPassword = digitsOnly;

        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.InputText($"{T("security.confirm")}##passcode-confirm", ref _editPasswordConfirm, 8, ImGuiInputTextFlags.Password);

        var confirmDigitsOnly = new string(_editPasswordConfirm.Where(char.IsDigit).ToArray());
        if (confirmDigitsOnly != _editPasswordConfirm) _editPasswordConfirm = confirmDigitsOnly;

        if (_editPassword.Length > 0 && _editPasswordConfirm.Length > 0
            && _editPassword != _editPasswordConfirm)
        {
            ImGui.TextColored(ColorRed, T("security.mismatch"));
        }

        if (!string.IsNullOrEmpty(_savedPasswordHash) && _editPassword.Length == 0)
            ImGui.TextColored(ColorMuted, T("security.keepCurrent"));

        if (isRunning)
        {
            ImGui.Spacing();
            if (ImGui.Button(T("security.invalidateSessions")))
                _plugin.Server.InvalidateAllSessions();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(T("security.invalidateTooltip"));
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Unsaved-changes warning
        if (HasUnsavedChanges())
            ImGui.TextColored(ColorYellow, T("common.unsavedChanges"));

        // ─── Buttons ──────────────────────────────────────────────────
        // Disable Save when a new passcode is partially entered but invalid
        var newPasscodeStarted = _editPassword.Length > 0 || _editPasswordConfirm.Length > 0;
        var canSave = !newPasscodeStarted || IsNewPasscodeValid();

        using (ImRaii.Disabled(!canSave))
        {
            if (ImGui.Button(T("common.save")))
                Save();

            ImGui.SameLine();

            if (ImGui.Button(T("common.saveAndClose")))
            {
                Save();
                IsOpen = false;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button(T("common.discard")))
        {
            SyncFromConfig();
            IsOpen = false;
        }
    }

    private static void SectionHeader(string text)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawUrlRow(string label, string url)
    {
        ImGui.TextColored(ColorMuted, $"  {label}:");
        ImGui.SameLine(110 * ImGuiHelpers.GlobalScale);

        if (ImGui.Selectable(url + $"##url-{label}"))
            Dalamud.Utility.Util.OpenLink(url);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(T("server.openInBrowser"));
    }
}
