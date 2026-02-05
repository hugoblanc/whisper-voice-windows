namespace WhisperVoice.Tray;

public enum AppState
{
    Idle,
    Recording,
    Transcribing
}

public class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly string _toggleShortcut;
    private readonly string _pttKey;

    private AppState _currentState = AppState.Idle;

    public event Action? QuitRequested;

    public TrayIcon(string toggleShortcut, string pttKey)
    {
        _toggleShortcut = toggleShortcut;
        _pttKey = pttKey;

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon("mic_idle"),
            Text = "Whisper Voice - Idle",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();

        contextMenu.Items.Add($"{_toggleShortcut} to toggle", null, null!);
        contextMenu.Items.Add($"{_pttKey} (hold) to record", null, null!);
        contextMenu.Items.Add(new ToolStripSeparator());

        _statusMenuItem = new ToolStripMenuItem("Status: Idle") { Enabled = false };
        contextMenu.Items.Add(_statusMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Version 2.2.0", null, null!) { Enabled = false };
        contextMenu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke());

        _notifyIcon.ContextMenuStrip = contextMenu;
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
        // Try to load from file first
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", $"{name}.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        // Generate simple colored icon programmatically
        return GenerateIcon(name);
    }

    private static Icon GenerateIcon(string name)
    {
        var color = name switch
        {
            "mic_recording" => Color.Red,
            "mic_transcribing" => Color.Orange,
            _ => Color.Gray // mic_idle
        };

        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);

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

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
