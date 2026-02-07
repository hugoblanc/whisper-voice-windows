using System.Runtime.InteropServices;
using WhisperVoice.Logging;

namespace WhisperVoice.Tray;

public enum AppState
{
    Idle,
    Recording,
    Transcribing
}

public class TrayIcon : IDisposable
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly ToolStripMenuItem _pttMenuItem;
    private readonly ToolStripMenuItem _modeMenuItem;
    private string _toggleShortcut;
    private string _pttKey;
    private string _currentMode;

    // Cache icons to avoid repeated generation and GDI handle leaks
    private static readonly Dictionary<string, Icon> _iconCache = new();
    private static readonly object _iconLock = new();

    private AppState _currentState = AppState.Idle;

    public event Action? QuitRequested;
    public event Action? PreferencesRequested;

    public TrayIcon(string toggleShortcut, string pttKey, string currentMode = "Brut", bool hasAIModes = false)
    {
        _toggleShortcut = toggleShortcut;
        _pttKey = pttKey;
        _currentMode = currentMode;

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon("mic_idle"),
            Text = "Whisper Voice - Idle",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();

        _toggleMenuItem = new ToolStripMenuItem($"{_toggleShortcut} to toggle") { Enabled = false };
        _pttMenuItem = new ToolStripMenuItem($"{_pttKey} (hold) to record") { Enabled = false };
        contextMenu.Items.Add(_toggleMenuItem);
        contextMenu.Items.Add(_pttMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        _statusMenuItem = new ToolStripMenuItem("Status: Idle") { Enabled = false };
        contextMenu.Items.Add(_statusMenuItem);

        // AI Mode indicator
        var modeText = hasAIModes
            ? $"Mode: {_currentMode} (Shift to switch)"
            : $"Mode: {_currentMode} (add OpenAI key for AI modes)";
        _modeMenuItem = new ToolStripMenuItem(modeText) { Enabled = false };
        contextMenu.Items.Add(_modeMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Preferences...", null, (_, _) => PreferencesRequested?.Invoke());
        contextMenu.Items.Add(new ToolStripSeparator());

        var versionItem = new ToolStripMenuItem($"Version {Logger.Version}") { Enabled = false };
        contextMenu.Items.Add(versionItem);
        contextMenu.Items.Add("Open Logs", null, (_, _) => Logger.OpenLogFile());
        contextMenu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke());

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    public void UpdateShortcutLabels(string toggleShortcut, string pttKey)
    {
        _toggleShortcut = toggleShortcut;
        _pttKey = pttKey;
        _toggleMenuItem.Text = $"{toggleShortcut} to toggle";
        _pttMenuItem.Text = $"{pttKey} (hold) to record";
    }

    public void UpdateModeLabel(string modeName)
    {
        _currentMode = modeName;
        _modeMenuItem.Text = $"Mode: {modeName} (Shift to switch)";
    }

    public void SetState(AppState state)
    {
        _currentState = state;

        switch (state)
        {
            case AppState.Idle:
                _notifyIcon.Icon = LoadIcon("mic_idle");
                _notifyIcon.Text = "Whisper Voice - Idle";
                _statusMenuItem.Text = "Status: Idle";
                break;

            case AppState.Recording:
                _notifyIcon.Icon = LoadIcon("mic_recording");
                _notifyIcon.Text = "Whisper Voice - Recording...";
                _statusMenuItem.Text = "Status: Recording...";
                break;

            case AppState.Transcribing:
                _notifyIcon.Icon = LoadIcon("mic_transcribing");
                _notifyIcon.Text = "Whisper Voice - Transcribing...";
                _statusMenuItem.Text = "Status: Transcribing...";
                break;
        }
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    private static Icon LoadIcon(string name)
    {
        lock (_iconLock)
        {
            // Return cached icon if available
            if (_iconCache.TryGetValue(name, out var cachedIcon))
            {
                return cachedIcon;
            }

            Icon icon;

            // Try to load from embedded resources first
            var assembly = typeof(TrayIcon).Assembly;
            var resourceName = $"{name}.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream != null)
            {
                icon = new Icon(stream);
            }
            else
            {
                // Fallback: try file path (for development)
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", $"{name}.ico");
                if (File.Exists(iconPath))
                {
                    icon = new Icon(iconPath);
                }
                else
                {
                    // Last resort: generate simple colored icon
                    icon = GenerateIcon(name);
                }
            }

            _iconCache[name] = icon;
            return icon;
        }
    }

    private static Icon GenerateIcon(string name)
    {
        var color = name switch
        {
            "mic_recording" => Color.Red,
            "mic_transcribing" => Color.Orange,
            _ => Color.Gray // mic_idle
        };

        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Draw microphone shape
            using var brush = new SolidBrush(color);

            // Mic head (rounded rectangle)
            g.FillEllipse(brush, 4, 1, 8, 8);

            // Mic stem
            g.FillRectangle(brush, 6, 9, 4, 3);

            // Mic base
            g.FillRectangle(brush, 4, 12, 8, 2);
        }

        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon).Clone() as Icon;
        DestroyIcon(hIcon); // Clean up the unmanaged handle
        bitmap.Dispose();

        return icon!;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        // Clean up cached icons
        lock (_iconLock)
        {
            foreach (var icon in _iconCache.Values)
            {
                icon.Dispose();
            }
            _iconCache.Clear();
        }
    }
}
