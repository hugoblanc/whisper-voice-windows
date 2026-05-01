using System.Drawing.Drawing2D;
using WhisperVoice.Logging;
using WhisperVoice.Tray;

namespace WhisperVoice.UI;

/// <summary>
/// Recording overlay with responsive layout for varied DPI and screen scaling.
/// </summary>
public class RecordingWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly System.Windows.Forms.Timer _waveformTimer;
    private readonly DateTime _startTime;
    private readonly float[] _waveformData;
    private int _waveformIndex;
    private AppState _state = AppState.Recording;

    private readonly Label _timerLabel;
    private readonly Label _statusLabel;
    private readonly Button _modeButton;
    private readonly Label _switchHintLabel;
    private readonly Panel _waveformPanel;
    private readonly Button _cancelButton;
    private bool _controlsReady;
    private readonly float _dpiScale;

    private static readonly Color RecordingColor = Color.FromArgb(239, 68, 68);
    private static readonly Color ProcessingColor = Color.FromArgb(59, 130, 246);
    private static readonly Color DoneColor = Color.FromArgb(34, 197, 94);
    private static readonly Color BackgroundColor = Color.FromArgb(28, 28, 28);
    private static readonly Color PanelColor = Color.FromArgb(42, 42, 42);
    private static readonly Color TextColor = Color.FromArgb(245, 245, 245);
    private static readonly Color MutedTextColor = Color.FromArgb(184, 184, 184);

    public event Action? CancelRequested;
    public event Action? ModeCycleRequested;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= WS_EX_NOACTIVATE;
            return createParams;
        }
    }

    public RecordingWindow()
    {
        _startTime = DateTime.Now;
        _waveformData = new float[120];
        _dpiScale = GetDpiScale();

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = ScaleSize(900, 360);
        MinimumSize = ScaleSize(760, 300);
        BackColor = BackgroundColor;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;

        _timerLabel = new Label
        {
            Text = "0:00",
            ForeColor = TextColor,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseCompatibleTextRendering = false
        };

        _statusLabel = new Label
        {
            Text = "Recording",
            ForeColor = RecordingColor,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            UseCompatibleTextRendering = false,
            AutoEllipsis = true
        };

        _modeButton = new Button
        {
            Text = "Mode: Brut",
            ForeColor = TextColor,
            BackColor = Color.FromArgb(54, 54, 54),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _modeButton.FlatAppearance.BorderColor = Color.FromArgb(86, 86, 86);
        _modeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
        _modeButton.Click += (_, _) => ModeCycleRequested?.Invoke();

        _switchHintLabel = new Label
        {
            Text = "Tab to switch",
            ForeColor = MutedTextColor,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        _waveformPanel = new DoubleBufferedPanel
        {
            BackColor = PanelColor
        };
        _waveformPanel.Paint += WaveformPanel_Paint;

        _cancelButton = new Button
        {
            Text = "Cancel",
            ForeColor = Color.FromArgb(220, 220, 220),
            BackColor = Color.FromArgb(60, 60, 60),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
        _cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 75, 75);
        _cancelButton.Click += (_, _) => CancelRequested?.Invoke();

        Controls.AddRange(new Control[]
        {
            _timerLabel,
            _statusLabel,
            _modeButton,
            _switchHintLabel,
            _waveformPanel,
            _cancelButton
        });
        _controlsReady = true;

        _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        _waveformTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _waveformTimer.Tick += (_, _) => _waveformPanel.Invalidate();
        _waveformTimer.Start();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                CancelRequested?.Invoke();
            }
        };

        LayoutControls();
        Logger.Debug($"RecordingWindow created: dpi={DeviceDpi}, scale={_dpiScale:0.##}, client={ClientSize.Width}x{ClientSize.Height}");
    }

    private static Region CreateRoundedRegion(int width, int height, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(width - diameter, height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    private void LayoutControls()
    {
        if (!_controlsReady) return;

        var width = Math.Max(ClientSize.Width, 1);
        var height = Math.Max(ClientSize.Height, 1);
        var compact = height < S(260) || width < S(700);
        var padding = compact
            ? Math.Clamp(width / 36, S(10), S(16))
            : Math.Clamp(width / 36, S(20), S(30));

        var buttonHeight = compact
            ? Math.Clamp(height / 5, S(28), S(34))
            : Math.Clamp(height / 8, S(38), S(48));
        var cancelWidth = Math.Clamp(width / 5, S(112), S(160));
        var cancelY = height - padding - buttonHeight;

        var headerHeight = compact
            ? Math.Clamp(height / 3, S(56), S(76))
            : Math.Clamp(height / 3, S(104), S(132));
        var rightWidth = Math.Clamp((int)(width * 0.38f), S(260), S(390));
        var rightX = width - padding - rightWidth;
        var timerWidth = Math.Max(S(220), rightX - padding - S(18));

        var timerFontSize = compact
            ? Math.Clamp(height / _dpiScale * 0.17f, 24f, 34f)
            : Math.Clamp(height / _dpiScale * 0.14f, 38f, 48f);
        var statusFontSize = compact ? 8.5f : 11f;
        var modeFontSize = compact ? 8.5f : 10f;
        var hintFontSize = compact ? 7.2f : 8.5f;

        SetFont(_timerLabel, timerFontSize, FontStyle.Bold);
        SetFont(_statusLabel, statusFontSize, FontStyle.Bold);
        SetFont(_modeButton, modeFontSize, FontStyle.Bold);
        SetFont(_switchHintLabel, hintFontSize, FontStyle.Regular);
        SetFont(_cancelButton, compact ? 8.5f : 10f, FontStyle.Regular);

        _timerLabel.SetBounds(padding, padding, timerWidth, headerHeight);

        var statusHeight = Math.Clamp(buttonHeight - S(2), S(22), S(34));
        _statusLabel.SetBounds(rightX, padding, rightWidth, statusHeight);

        var modeY = padding + statusHeight + (compact ? S(3) : S(8));
        var modeHeight = Math.Clamp(buttonHeight, S(28), S(42));
        _modeButton.SetBounds(rightX, modeY, rightWidth, modeHeight);

        var hintY = modeY + modeHeight + S(2);
        var hintHeight = Math.Max(S(16), Math.Min(S(26), cancelY - hintY));
        _switchHintLabel.Visible = hintHeight >= 14 && !compact;
        _switchHintLabel.SetBounds(rightX, hintY, rightWidth, hintHeight);

        var waveformY = padding + headerHeight + (compact ? S(8) : S(16));
        var waveformBottom = cancelY - (compact ? S(10) : S(18));
        var waveformHeight = Math.Max(S(32), waveformBottom - waveformY);
        _waveformPanel.SetBounds(padding, waveformY, width - padding * 2, waveformHeight);

        _cancelButton.SetBounds(width - padding - cancelWidth, cancelY, cancelWidth, buttonHeight);

        Region?.Dispose();
        Region = CreateRoundedRegion(width, height, compact ? 12 : 16);
    }

    private float GetDpiScale()
    {
        try
        {
            using var graphics = CreateGraphics();
            return Math.Clamp(graphics.DpiX / 96f, 1f, 3f);
        }
        catch
        {
            return 1f;
        }
    }

    private int S(int value) => Math.Max(1, (int)Math.Round(value * _dpiScale));

    private Size ScaleSize(int width, int height) => new(S(width), S(height));

    private static void SetFont(Control control, float size, FontStyle style)
    {
        if (Math.Abs(control.Font.Size - size) < 0.1f && control.Font.Style == style) return;
        control.Font = new Font("Segoe UI", size, style);
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _startTime;
        _timerLabel.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
    }

    public void UpdateAudioLevel(float level)
    {
        if (_state != AppState.Recording) return;

        _waveformData[_waveformIndex] = Math.Clamp(level, 0.06f, 1f);
        _waveformIndex = (_waveformIndex + 1) % _waveformData.Length;
    }

    private void WaveformPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(_waveformPanel.BackColor);

        var width = _waveformPanel.Width;
        var height = _waveformPanel.Height;
        if (width <= 0 || height <= 0) return;

        var barWidth = Math.Max(2f, width / (float)_waveformData.Length);
        var centerY = height / 2f;
        var color = _state switch
        {
            AppState.Recording => RecordingColor,
            AppState.Transcribing => ProcessingColor,
            _ => DoneColor
        };

        using var brush = new SolidBrush(color);

        for (var i = 0; i < _waveformData.Length; i++)
        {
            var dataIndex = (_waveformIndex + i) % _waveformData.Length;
            var level = _waveformData[dataIndex];
            var barHeight = Math.Max(2f, level * height * 0.78f);
            var x = i * barWidth;
            var y = centerY - barHeight / 2;

            g.FillRectangle(brush, x + 1, y, Math.Max(1f, barWidth - 2), barHeight);
        }
    }

    public void SetMode(string modeName)
    {
        var text = $"Mode: {modeName}";

        if (InvokeRequired)
        {
            Invoke(() => _modeButton.Text = text);
        }
        else
        {
            _modeButton.Text = text;
        }
    }

    public void SetState(AppState state)
    {
        _state = state;

        var (text, color) = state switch
        {
            AppState.Recording => ("Recording", RecordingColor),
            AppState.Transcribing => ("Processing", ProcessingColor),
            _ => ("Done", DoneColor)
        };

        if (InvokeRequired)
        {
            Invoke(() => ApplyState(text, color));
        }
        else
        {
            ApplyState(text, color);
        }

        _waveformPanel.Invalidate();
    }

    private void ApplyState(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
        _modeButton.Enabled = _state == AppState.Recording;
        _switchHintLabel.Enabled = _state == AppState.Recording;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutControls();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _updateTimer.Stop();
        _updateTimer.Dispose();
        _waveformTimer.Stop();
        _waveformTimer.Dispose();
        Region?.Dispose();
        base.OnFormClosed(e);
    }

    private class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
