using System.Drawing.Drawing2D;
using WhisperVoice.Processing;
using WhisperVoice.Tray;

namespace WhisperVoice.UI;

/// <summary>
/// Recording window with waveform visualization, timer, and mode display
/// </summary>
public class RecordingWindow : Form
{
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly System.Windows.Forms.Timer _waveformTimer;
    private readonly DateTime _startTime;
    private readonly float[] _waveformData;
    private int _waveformIndex;
    private AppState _state = AppState.Recording;
    private string _modeName = "Brut";

    // UI Controls
    private readonly Label _timerLabel;
    private readonly Label _modeLabel;
    private readonly Label _statusLabel;
    private readonly Panel _waveformPanel;
    private readonly Button _cancelButton;

    // Colors
    private static readonly Color RecordingColor = Color.FromArgb(239, 68, 68);  // Red
    private static readonly Color ProcessingColor = Color.FromArgb(59, 130, 246); // Blue
    private static readonly Color DoneColor = Color.FromArgb(34, 197, 94);        // Green
    private static readonly Color BackgroundColor = Color.FromArgb(30, 30, 30);
    private static readonly Color TextColor = Color.FromArgb(240, 240, 240);

    public event Action? CancelRequested;

    public RecordingWindow()
    {
        _startTime = DateTime.Now;
        _waveformData = new float[100];

        // Window settings
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(400, 220);
        BackColor = BackgroundColor;
        ShowInTaskbar = false;
        TopMost = true;

        // Add rounded corners
        Region = CreateRoundedRegion(Width, Height, 16);

        // Timer label
        _timerLabel = new Label
        {
            Text = "0:00",
            Font = new Font("Segoe UI", 32, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = true,
            Location = new Point(20, 20)
        };

        // Mode label
        _modeLabel = new Label
        {
            Text = "Brut",
            Font = new Font("Segoe UI", 12),
            ForeColor = Color.FromArgb(160, 160, 160),
            AutoSize = true,
            Location = new Point(22, 75)
        };

        // Status indicator
        _statusLabel = new Label
        {
            Text = "● Recording",
            Font = new Font("Segoe UI", 11),
            ForeColor = RecordingColor,
            AutoSize = false,
            Size = new Size(130, 30),
            Location = new Point(Width - 150, 25),
            TextAlign = ContentAlignment.TopRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        // Waveform panel
        _waveformPanel = new DoubleBufferedPanel
        {
            Location = new Point(20, 105),
            Size = new Size(Width - 40, 60),
            BackColor = Color.FromArgb(45, 45, 45),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _waveformPanel.Paint += WaveformPanel_Paint;

        // Cancel button
        _cancelButton = new Button
        {
            Text = "Cancel (Esc)",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(60, 60, 60),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(110, 32),
            Location = new Point(Width - 130, 175),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        _cancelButton.Click += (_, _) => CancelRequested?.Invoke();

        // Add controls
        Controls.AddRange(new Control[] { _timerLabel, _modeLabel, _statusLabel, _waveformPanel, _cancelButton });

        // Update timer (for elapsed time display)
        _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        // Waveform animation timer
        _waveformTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _waveformTimer.Tick += WaveformTimer_Tick;
        _waveformTimer.Start();

        // Handle Escape key
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                CancelRequested?.Invoke();
            }
        };
    }

    private static Region CreateRoundedRegion(int width, int height, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
        path.AddArc(width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
        path.AddArc(width - radius * 2, height - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(0, height - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _startTime;
        _timerLabel.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
    }

    private void WaveformTimer_Tick(object? sender, EventArgs e)
    {
        // Repaint waveform (data is updated via UpdateAudioLevel from real microphone)
        _waveformPanel.Invalidate();
    }

    /// <summary>
    /// Update with actual audio level from recorder
    /// </summary>
    public void UpdateAudioLevel(float level)
    {
        if (_state == AppState.Recording)
        {
            _waveformData[_waveformIndex] = Math.Clamp(level, 0.1f, 1f);
            _waveformIndex = (_waveformIndex + 1) % _waveformData.Length;
        }
    }

    private void WaveformPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var width = _waveformPanel.Width;
        var height = _waveformPanel.Height;
        var barWidth = width / (float)_waveformData.Length;
        var centerY = height / 2f;

        var color = _state switch
        {
            AppState.Recording => RecordingColor,
            AppState.Transcribing => ProcessingColor,
            _ => DoneColor
        };

        using var brush = new SolidBrush(color);

        for (int i = 0; i < _waveformData.Length; i++)
        {
            var dataIndex = (_waveformIndex + i) % _waveformData.Length;
            var level = _waveformData[dataIndex];
            var barHeight = level * height * 0.8f;

            var x = i * barWidth;
            var y = centerY - barHeight / 2;

            g.FillRectangle(brush, x + 1, y, barWidth - 2, barHeight);
        }
    }

    /// <summary>
    /// Update the displayed mode name
    /// </summary>
    public void SetMode(string modeName)
    {
        _modeName = modeName;
        if (InvokeRequired)
            Invoke(() => _modeLabel.Text = modeName);
        else
            _modeLabel.Text = modeName;
    }

    /// <summary>
    /// Update the recording state
    /// </summary>
    public void SetState(AppState state)
    {
        _state = state;

        var (text, color) = state switch
        {
            AppState.Recording => ("● Recording", RecordingColor),
            AppState.Transcribing => ("● Processing", ProcessingColor),
            _ => ("● Done", DoneColor)
        };

        if (InvokeRequired)
        {
            Invoke(() =>
            {
                _statusLabel.Text = text;
                _statusLabel.ForeColor = color;
            });
        }
        else
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
        }

        // Trigger repaint for waveform color change
        _waveformPanel.Invalidate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _updateTimer.Stop();
        _updateTimer.Dispose();
        _waveformTimer.Stop();
        _waveformTimer.Dispose();
        base.OnFormClosed(e);
    }

    /// <summary>
    /// Double-buffered panel to prevent flickering
    /// </summary>
    private class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
