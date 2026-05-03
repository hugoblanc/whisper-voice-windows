using System.Drawing.Drawing2D;
using WhisperVoice.Logging;
using WhisperVoice.Tray;

namespace WhisperVoice.UI;

/// <summary>
/// Floating recording HUD inspired by the macOS Whisper Voice recorder.
/// </summary>
public class RecordingWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly System.Windows.Forms.Timer _paintTimer;
    private readonly DateTime _startTime;
    private readonly Screen _targetScreen;

    private readonly WaveformHud _waveform;
    private readonly Label _statusLabel;
    private readonly Label _timerLabel;
    private readonly ModeSelectorHud _modeSelector;
    private readonly Label _switchHintLabel;
    private readonly ProjectChipHud _projectChip;
    private readonly CapsuleHudButton _cancelButton;
    private readonly CapsuleHudButton _stopButton;

    private AppState _state = AppState.Recording;
    private float _latestAudioLevel;
    private float _haloPhase;
    private bool _controlsReady;

    private static readonly Color RecordingColor = Color.FromArgb(255, 59, 69);
    private static readonly Color ProcessingColor = Color.FromArgb(72, 162, 255);
    private static readonly Color DoneColor = Color.FromArgb(68, 211, 123);
    private static readonly Color GlassTopColor = Color.FromArgb(117, 190, 211);
    private static readonly Color GlassBottomColor = Color.FromArgb(95, 143, 179);
    private static readonly Color TextColor = Color.FromArgb(248, 251, 255);
    private static readonly Color MutedTextColor = Color.FromArgb(206, 226, 238);

    public event Action? CancelRequested;
    public event Action? StopRequested;
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

    public RecordingWindow(IntPtr targetWindow = default)
    {
        _startTime = DateTime.Now;
        _targetScreen = targetWindow != IntPtr.Zero
            ? Screen.FromHandle(targetWindow)
            : Screen.PrimaryScreen ?? Screen.AllScreens.First();

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ClientSize = GetInitialClientSize(_targetScreen.WorkingArea);
        MinimumSize = new Size(620, 380);
        BackColor = GlassBottomColor;
        Opacity = 0.98;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;

        _waveform = new WaveformHud();

        _statusLabel = new Label
        {
            Text = "Recording",
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseCompatibleTextRendering = false,
            AutoEllipsis = true
        };

        _timerLabel = new Label
        {
            Text = "0:00",
            ForeColor = MutedTextColor,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            UseCompatibleTextRendering = false,
            AutoEllipsis = true
        };

        _modeSelector = new ModeSelectorHud { ModeName = "Brut" };
        _modeSelector.Click += (_, _) => ModeCycleRequested?.Invoke();

        _switchHintLabel = new Label
        {
            Text = "Tab to switch",
            ForeColor = Color.FromArgb(170, 222, 240, 248),
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseCompatibleTextRendering = false,
            AutoEllipsis = true
        };

        _projectChip = new ProjectChipHud();

        _cancelButton = new CapsuleHudButton("Cancel", CapsuleIcon.Cancel, isPrimary: false);
        _cancelButton.Click += (_, _) => CancelRequested?.Invoke();

        _stopButton = new CapsuleHudButton("Stop", CapsuleIcon.Stop, isPrimary: true);
        _stopButton.Click += (_, _) => StopRequested?.Invoke();

        Controls.AddRange(new Control[]
        {
            _waveform,
            _statusLabel,
            _timerLabel,
            _modeSelector,
            _switchHintLabel,
            _projectChip,
            _cancelButton,
            _stopButton
        });
        _controlsReady = true;

        _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        _paintTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _paintTimer.Tick += (_, _) =>
        {
            _haloPhase += 0.16f;
            Invalidate();
            _waveform.Invalidate();
        };
        _paintTimer.Start();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                CancelRequested?.Invoke();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                StopRequested?.Invoke();
                e.Handled = true;
            }
        };

        PlaceOnTargetScreen();
        LayoutControls();
        Logger.Debug($"RecordingWindow created: dpi={DeviceDpi}, client={ClientSize.Width}x{ClientSize.Height}, bounds={Bounds}, screen={_targetScreen.WorkingArea}");
    }

    private void LayoutControls()
    {
        if (!_controlsReady) return;

        var width = Math.Max(ClientSize.Width, 1);
        var height = Math.Max(ClientSize.Height, 1);
        var scale = Math.Clamp(Math.Min(width / 720f, height / 468f), 0.68f, 1.05f);
        int S(float value) => Math.Max(1, (int)Math.Round(value * scale));

        var padding = S(28);
        var waveformWidth = Math.Min(S(568), width - padding * 2);
        var waveformX = (width - waveformWidth) / 2;
        _waveform.SetBounds(waveformX, S(38), waveformWidth, S(74));

        var statusY = S(134);
        var timerWidth = S(96);
        var statusLeft = padding + S(50);
        var statusRight = width - padding - timerWidth - S(18);
        _statusLabel.SetBounds(statusLeft, statusY, Math.Max(S(112), statusRight - statusLeft), S(38));
        _timerLabel.SetBounds(width - padding - timerWidth, statusY, timerWidth, S(38));

        SetFittedLabelFont(_statusLabel, "Segoe UI", FontStyle.Bold, S(28), S(16));
        SetFittedLabelFont(_timerLabel, "Consolas", FontStyle.Bold, S(28), S(16));

        var modeY = S(184);
        var hintGap = S(22);
        var hintMinWidth = S(100);
        var modeHeight = S(70);
        var maxModeWidth = width - padding * 2 - hintGap - hintMinWidth;
        var modeWidth = Math.Clamp(Math.Min(S(366), maxModeWidth), S(255), S(366));
        _modeSelector.SetBounds(padding, modeY, modeWidth, modeHeight);

        var hintWidth = width - _modeSelector.Right - padding - hintGap;
        _switchHintLabel.Visible = hintWidth >= S(58);
        _switchHintLabel.Text = hintWidth < S(105) ? "Tab" : "Tab to switch";
        _switchHintLabel.SetBounds(
            _modeSelector.Right + hintGap,
            modeY + (modeHeight - S(24)) / 2,
            Math.Max(S(58), hintWidth),
            S(24));
        SetFittedLabelFont(_switchHintLabel, "Segoe UI", FontStyle.Bold, S(20), S(12));

        var buttonHeight = S(62);
        var buttonWidth = Math.Clamp((width - S(72)) / 3, S(128), S(190));
        var buttonY = height - S(82);
        _cancelButton.SetBounds(S(36), buttonY, buttonWidth, buttonHeight);
        _stopButton.SetBounds(width - S(36) - buttonWidth, buttonY, buttonWidth, buttonHeight);

        var chipHeight = S(58);
        var chipY = Math.Max(_modeSelector.Bottom + S(18), buttonY - S(70));
        _projectChip.SetBounds(padding, chipY, width - padding * 2, chipHeight);

        Region?.Dispose();
        Region = CreateRoundedRegion(width, height, S(34));
    }

    private static void SetFittedLabelFont(Label label, string family, FontStyle style, float maxPixels, float minPixels)
    {
        if (label.Width <= 0 || label.Height <= 0) return;

        using var graphics = label.CreateGraphics();
        var size = FitTextPixelSize(
            graphics,
            label.Text,
            family,
            style,
            new SizeF(label.Width - 2, label.Height - 2),
            maxPixels,
            minPixels);

        if (Math.Abs(label.Font.Size - size) < 0.1f &&
            label.Font.Style == style &&
            label.Font.Unit == GraphicsUnit.Pixel &&
            string.Equals(label.Font.FontFamily.Name, family, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        label.Font = new Font(family, size, style, GraphicsUnit.Pixel);
    }

    private static Size GetInitialClientSize(Rectangle workingArea)
    {
        var width = Math.Clamp((int)(workingArea.Width * 0.23f), 680, 760);
        var height = Math.Clamp((int)(workingArea.Height * 0.23f), 430, 480);
        return new Size(width, height);
    }

    private void PlaceOnTargetScreen()
    {
        var area = _targetScreen.WorkingArea;
        var x = area.Left + Math.Max(12, (area.Width - Width) / 2);
        var y = area.Top + Math.Max(12, (area.Height - Height) / 2);

        if (x + Width > area.Right - 12)
        {
            x = Math.Max(area.Left + 12, area.Right - Width - 12);
        }

        if (y + Height > area.Bottom - 12)
        {
            y = Math.Max(area.Top + 12, area.Bottom - Height - 12);
        }

        Location = new Point(x, y);
    }

    private void EnsureVisibleTopMost()
    {
        PlaceOnTargetScreen();
        SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        Logger.Debug($"RecordingWindow shown: bounds={Bounds}, visible={Visible}, topMost={TopMost}");
    }

    private static Region CreateRoundedRegion(int width, int height, int radius)
    {
        using var path = RoundedRect(new Rectangle(0, 0, width, height), radius);
        return new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bounds = ClientRectangle;
        var radius = Math.Max(18, Math.Min(bounds.Width, bounds.Height) / 14);
        using var glassPath = RoundedRect(bounds, radius);
        using var gradient = new LinearGradientBrush(bounds, GlassTopColor, GlassBottomColor, LinearGradientMode.Vertical);
        g.FillPath(gradient, glassPath);

        using (var tint = new SolidBrush(Color.FromArgb(34, 255, 255, 255)))
        {
            g.FillPath(tint, glassPath);
        }

        using (var borderPen = new Pen(Color.FromArgb(100, 34, 76, 96), 2f))
        using (var highlightPen = new Pen(Color.FromArgb(95, 255, 255, 255), 1f))
        {
            g.DrawPath(borderPen, glassPath);
            using var innerPath = RoundedRect(Rectangle.Inflate(bounds, -2, -2), Math.Max(2, radius - 2));
            g.DrawPath(highlightPen, innerPath);
        }

        DrawStatusDot(g);
    }

    private void DrawStatusDot(Graphics g)
    {
        var scale = Math.Clamp(Math.Min(ClientSize.Width / 720f, ClientSize.Height / 468f), 0.68f, 1.05f);
        int S(float value) => Math.Max(1, (int)Math.Round(value * scale));

        var center = new Point(S(47), _statusLabel.Top + _statusLabel.Height / 2);
        var color = _state switch
        {
            AppState.Recording => RecordingColor,
            AppState.Transcribing => ProcessingColor,
            _ => DoneColor
        };

        var idlePulse = _state == AppState.Recording ? (float)((Math.Sin(_haloPhase) + 1) * 0.5) : 0f;
        var glow = Math.Clamp(_latestAudioLevel * 0.8f + idlePulse * 0.18f, 0f, 1f);
        var haloSize = S(26 + glow * 10);
        var haloRect = new Rectangle(center.X - haloSize / 2, center.Y - haloSize / 2, haloSize, haloSize);
        using (var haloBrush = new SolidBrush(Color.FromArgb((int)(35 + glow * 95), color)))
        {
            g.FillEllipse(haloBrush, haloRect);
        }

        var dotSize = S(20);
        var dotRect = new Rectangle(center.X - dotSize / 2, center.Y - dotSize / 2, dotSize, dotSize);
        using var dotBrush = new SolidBrush(color);
        g.FillEllipse(dotBrush, dotRect);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static float FitTextPixelSize(
        Graphics graphics,
        string text,
        string family,
        FontStyle style,
        SizeF available,
        float maxPixels,
        float minPixels)
    {
        if (string.IsNullOrEmpty(text)) return minPixels;

        maxPixels = Math.Max(minPixels, maxPixels);
        for (var size = maxPixels; size >= minPixels; size -= 1f)
        {
            using var font = new Font(family, size, style, GraphicsUnit.Pixel);
            var measured = graphics.MeasureString(text, font, PointF.Empty, NoWrapStringFormat);
            if (measured.Width <= available.Width && measured.Height <= available.Height)
            {
                return size;
            }
        }

        return minPixels;
    }

    private static readonly StringFormat NoWrapStringFormat = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _startTime;
        _timerLabel.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
    }

    public void UpdateAudioLevel(float level)
    {
        if (_state != AppState.Recording) return;

        _latestAudioLevel = Math.Clamp(level, 0f, 1f);
        _waveform.AddLevel(_latestAudioLevel);
    }

    public void SetMode(string modeName)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetMode(modeName));
            return;
        }

        _modeSelector.ModeName = modeName;
    }

    public void SetState(AppState state)
    {
        _state = state;

        if (InvokeRequired)
        {
            Invoke(() => ApplyState());
        }
        else
        {
            ApplyState();
        }
    }

    public void SetStarting()
    {
        if (InvokeRequired)
        {
            Invoke(SetStarting);
            return;
        }

        _statusLabel.Text = "Starting";
        _waveform.BarBaseColor = ProcessingColor;
        _modeSelector.Enabled = false;
        _stopButton.Enabled = false;
        _cancelButton.Enabled = true;
        _switchHintLabel.Enabled = false;
        SetFittedLabelFont(_statusLabel, "Segoe UI", FontStyle.Bold, Math.Max(18, _statusLabel.Height * 0.72f), 12);
        Invalidate();
        _waveform.Invalidate();
    }

    private void ApplyState()
    {
        var color = _state switch
        {
            AppState.Recording => RecordingColor,
            AppState.Transcribing => ProcessingColor,
            _ => DoneColor
        };

        _statusLabel.Text = _state switch
        {
            AppState.Recording => "Recording",
            AppState.Transcribing => "Processing",
            _ => "Done"
        };

        _waveform.BarBaseColor = color;
        _modeSelector.Enabled = _state == AppState.Recording;
        _stopButton.Enabled = _state == AppState.Recording;
        _cancelButton.Enabled = _state == AppState.Recording;
        _switchHintLabel.Enabled = _state == AppState.Recording;
        Invalidate();
        _waveform.Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutControls();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        EnsureVisibleTopMost();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _updateTimer.Stop();
        _updateTimer.Dispose();
        _paintTimer.Stop();
        _paintTimer.Dispose();
        Region?.Dispose();
        base.OnFormClosed(e);
    }

    private sealed class WaveformHud : Control
    {
        private readonly float[] _levels = new float[48];
        private readonly float[] _smoothedLevels = new float[48];
        private int _index;

        public Color BarBaseColor { get; set; } = RecordingColor;

        public WaveformHud()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        public void AddLevel(float level)
        {
            _levels[_index] = Math.Clamp(level, 0.02f, 1f);

            for (var i = 0; i < _smoothedLevels.Length; i++)
            {
                var target = _levels[i];
                var current = _smoothedLevels[i];
                var factor = target > current ? 0.62f : 0.12f;
                _smoothedLevels[i] = current + (target - current) * factor;
            }

            _index = (_index + 1) % _levels.Length;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Width <= 0 || Height <= 0) return;

            var count = _smoothedLevels.Length;
            var barWidth = Math.Clamp(Width / (count * 2.05f), 3f, 6f);
            var spacing = barWidth * 1.05f;
            var totalWidth = count * barWidth + (count - 1) * spacing;
            var startX = (Width - totalWidth) / 2f;
            var centerY = Height / 2f;
            var minHeight = Math.Max(4f, Height * 0.09f);

            for (var i = 0; i < count; i++)
            {
                var displayIndex = (_index + i) % count;
                var level = _smoothedLevels[displayIndex];
                var boosted = Math.Pow(level, 0.58);
                var barHeight = (float)Math.Max(minHeight, boosted * Height * 0.92);
                var x = startX + i * (barWidth + spacing);
                var y = centerY - barHeight / 2f;
                var rect = new RectangleF(x, y, barWidth, barHeight);

                var color = GetBarColor(level);
                if (level > 0.5f)
                {
                    using var glowBrush = new SolidBrush(Color.FromArgb(55, color));
                    using var glowPath = RoundedRectF(RectangleF.Inflate(rect, 2f, 2f), barWidth + 2f);
                    g.FillPath(glowBrush, glowPath);
                }

                using var brush = new SolidBrush(color);
                using var path = RoundedRectF(rect, barWidth / 2f);
                g.FillPath(brush, path);
            }
        }

        private Color GetBarColor(float level)
        {
            if (level > 0.70f) return Color.FromArgb(255, 255, 119, 42);
            if (level > 0.40f)
            {
                var t = (level - 0.40f) / 0.30f;
                return Blend(BarBaseColor, Color.FromArgb(255, 255, 105, 42), t);
            }

            return BarBaseColor;
        }
    }

    private sealed class ModeSelectorHud : Control
    {
        private string _modeName = "Brut";

        public string ModeName
        {
            get => _modeName;
            set
            {
                _modeName = string.IsNullOrWhiteSpace(value) ? "Brut" : value;
                Invalidate();
            }
        }

        public ModeSelectorHud()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedRect(rect, Math.Max(12, Height / 3)))
            using (var fill = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
            using (var border = new Pen(Color.FromArgb(34, 255, 255, 255), 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            var selectedWidth = Math.Clamp((int)(Width * 0.48f), Math.Min(118, Width - 24), 180);
            var selectedHeight = Math.Max(30, Height - 20);
            var iconLaneWidth = Math.Min(Width / 2, Height * 2 + 10);
            var selectedX = Math.Max(Width - selectedWidth - 14, iconLaneWidth);
            var selectedY = (Height - selectedHeight) / 2;

            var iconSize = Math.Clamp(Height / 3, 16, 24);
            DrawWaveformIcon(g, new Rectangle(Width / 10, Height / 2 - iconSize / 2, iconSize, iconSize), Color.FromArgb(180, 242, 250, 255));
            DrawBoltIcon(g, new Rectangle(Width / 10 + Height, Height / 2 - iconSize / 2, iconSize, iconSize), Color.FromArgb(200, 242, 250, 255));

            var selectedRect = new Rectangle(selectedX, selectedY, selectedWidth, selectedHeight);
            using (var path = RoundedRect(selectedRect, Math.Max(10, selectedHeight / 4)))
            using (var fill = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
            using (var border = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            var modeIconSize = Math.Clamp(selectedRect.Height / 2, 14, 22);
            var iconRect = new Rectangle(selectedRect.Left + Math.Max(10, selectedRect.Height / 3), selectedRect.Top + selectedRect.Height / 2 - modeIconSize / 2, modeIconSize, modeIconSize);
            DrawModeIcon(g, iconRect, ModeName, Color.White);

            using var textBrush = new SolidBrush(Color.White);
            var textRect = new RectangleF(iconRect.Right + Math.Max(7, selectedRect.Height / 5), selectedRect.Top, selectedRect.Right - iconRect.Right - Math.Max(14, selectedRect.Height / 3), selectedRect.Height);
            var fontSize = FitTextPixelSize(g, ModeName, "Segoe UI", FontStyle.Bold, textRect.Size, Math.Max(16, selectedRect.Height * 0.54f), 12);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(ModeName, font, textBrush, textRect, stringFormat);
        }
    }

    private sealed class ProjectChipHud : Control
    {
        public ProjectChipHud()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Math.Max(10, Height / 5)))
            using (var fill = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
            {
                g.FillPath(fill, path);
            }

            var fullText = "in: (untagged)   click to pick";
            var fontSize = FitTextPixelSize(g, fullText, "Segoe UI", FontStyle.Bold, new SizeF(Width - 40, Height - 6), Math.Max(17, Height * 0.46f), 12);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var muted = new SolidBrush(Color.FromArgb(180, 232, 246, 255));
            using var primary = new SolidBrush(Color.FromArgb(218, 255, 255, 255));
            var x = 20f;
            var y = Height / 2f - font.GetHeight(g) / 2f - 1f;

            g.DrawString("in: ", font, muted, x, y);
            x += g.MeasureString("in: ", font).Width;
            g.DrawString("(untagged)", font, primary, x, y);
            x += g.MeasureString("(untagged)   ", font).Width;
            var remaining = Width - x - 18;
            if (remaining > g.MeasureString("pick", font).Width)
            {
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString("click to pick", font, muted, new RectangleF(x, 0, remaining, Height), format);
            }
        }
    }

    private sealed class CapsuleHudButton : Control
    {
        private readonly string _title;
        private readonly CapsuleIcon _icon;
        private readonly bool _isPrimary;
        private bool _isHovering;
        private bool _isPressed;

        public CapsuleHudButton(string title, CapsuleIcon icon, bool isPrimary)
        {
            _title = title;
            _icon = icon;
            _isPrimary = isPrimary;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.Selectable,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovering = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var fillAlpha = _isPrimary
                ? (_isPressed ? 92 : _isHovering ? 78 : 60)
                : (_isPressed ? 50 : _isHovering ? 38 : 26);
            var borderAlpha = _isPrimary ? 105 : 66;

            if (!Enabled)
            {
                fillAlpha /= 2;
                borderAlpha /= 2;
            }

            using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
            using (var fill = new SolidBrush(Color.FromArgb(fillAlpha, 255, 255, 255)))
            using (var border = new Pen(Color.FromArgb(borderAlpha, 255, 255, 255), 2f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            var textColor = Enabled
                ? Color.FromArgb(_isPrimary ? 255 : 232, 255, 255, 255)
                : Color.FromArgb(120, 255, 255, 255);
            var iconSize = Math.Clamp(Height / 4, 12, 22);
            var iconRect = new Rectangle(Math.Max(14, Width / 6), Height / 2 - iconSize / 2, iconSize, iconSize);

            var textRect = new RectangleF(iconRect.Right + Math.Max(8, Width * 0.07f), 0, Width - iconRect.Right - Width * 0.13f, Height);
            var showText = textRect.Width >= Math.Max(42, Height * 0.86f);
            if (!showText)
            {
                iconRect = new Rectangle(Width / 2 - iconSize / 2, Height / 2 - iconSize / 2, iconSize, iconSize);
                DrawCapsuleIcon(g, iconRect, _icon, textColor);
                return;
            }

            DrawCapsuleIcon(g, iconRect, _icon, textColor);
            using var brush = new SolidBrush(textColor);
            var fontSize = FitTextPixelSize(g, _title, "Segoe UI", FontStyle.Bold, textRect.Size, Math.Max(16, Height * 0.42f), 11);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(_title, font, brush, textRect, format);
        }
    }

    private enum CapsuleIcon
    {
        Cancel,
        Stop
    }

    private static void DrawCapsuleIcon(Graphics g, Rectangle rect, CapsuleIcon icon, Color color)
    {
        using var pen = new Pen(color, Math.Max(2f, rect.Width / 8f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (icon == CapsuleIcon.Cancel)
        {
            g.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Bottom);
            g.DrawLine(pen, rect.Right, rect.Top, rect.Left, rect.Bottom);
            return;
        }

        using var brush = new SolidBrush(color);
        var square = Rectangle.Inflate(rect, -1, -1);
        using var path = RoundedRect(square, Math.Max(2, square.Width / 5));
        g.FillPath(brush, path);
    }

    private static void DrawModeIcon(Graphics g, Rectangle rect, string modeName, Color color)
    {
        var normalized = modeName.Trim().ToLowerInvariant();
        if (normalized.Contains("brut"))
        {
            DrawWaveformIcon(g, rect, color);
        }
        else if (normalized.Contains("super"))
        {
            DrawBoltIcon(g, rect, color);
        }
        else if (normalized.Contains("clean"))
        {
            DrawSparkleIcon(g, rect, color);
        }
        else if (normalized.Contains("formel") || normalized.Contains("formal"))
        {
            DrawBriefcaseIcon(g, rect, color);
        }
        else if (normalized.Contains("markdown"))
        {
            DrawTextIcon(g, rect, color);
        }
        else
        {
            DrawEnvelopeIcon(g, rect, color);
        }
    }

    private static void DrawEnvelopeIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.6f, rect.Width / 12f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var box = Rectangle.Inflate(rect, -1, -2);
        g.DrawRectangle(pen, box);
        g.DrawLine(pen, box.Left, box.Top, box.Left + box.Width / 2, box.Top + box.Height / 2);
        g.DrawLine(pen, box.Right, box.Top, box.Left + box.Width / 2, box.Top + box.Height / 2);
    }

    private static void DrawWaveformIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.5f, rect.Width / 12f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var bars = new[] { 0.34f, 0.70f, 0.45f, 0.95f, 0.52f };
        for (var i = 0; i < bars.Length; i++)
        {
            var x = rect.Left + rect.Width * (i + 1) / (float)(bars.Length + 1);
            var h = rect.Height * bars[i];
            g.DrawLine(pen, x, rect.Top + (rect.Height - h) / 2f, x, rect.Top + (rect.Height + h) / 2f);
        }
    }

    private static void DrawBoltIcon(Graphics g, Rectangle rect, Color color)
    {
        var points = new[]
        {
            new PointF(rect.Left + rect.Width * 0.60f, rect.Top),
            new PointF(rect.Left + rect.Width * 0.25f, rect.Top + rect.Height * 0.55f),
            new PointF(rect.Left + rect.Width * 0.52f, rect.Top + rect.Height * 0.55f),
            new PointF(rect.Left + rect.Width * 0.38f, rect.Bottom),
            new PointF(rect.Left + rect.Width * 0.78f, rect.Top + rect.Height * 0.42f),
            new PointF(rect.Left + rect.Width * 0.50f, rect.Top + rect.Height * 0.42f)
        };
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, points);
    }

    private static void DrawSparkleIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.5f, rect.Width / 12f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var cx = rect.Left + rect.Width * 0.45f;
        var cy = rect.Top + rect.Height * 0.45f;
        g.DrawLine(pen, cx, rect.Top, cx, rect.Bottom);
        g.DrawLine(pen, rect.Left, cy, rect.Right, cy);
        g.DrawLine(pen, rect.Left + rect.Width * 0.20f, rect.Top + rect.Height * 0.20f, rect.Right - rect.Width * 0.20f, rect.Bottom - rect.Height * 0.20f);
        g.DrawLine(pen, rect.Right - rect.Width * 0.20f, rect.Top + rect.Height * 0.20f, rect.Left + rect.Width * 0.20f, rect.Bottom - rect.Height * 0.20f);
    }

    private static void DrawBriefcaseIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.5f, rect.Width / 13f)) { LineJoin = LineJoin.Round };
        var body = new Rectangle(rect.Left + 1, rect.Top + rect.Height / 3, rect.Width - 2, rect.Height * 2 / 3 - 1);
        g.DrawRectangle(pen, body);
        g.DrawArc(pen, rect.Left + rect.Width / 3, rect.Top + 1, rect.Width / 3, rect.Height / 2, 180, 180);
    }

    private static void DrawTextIcon(Graphics g, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, Math.Max(1.5f, rect.Width / 12f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(pen, rect.Left, rect.Top + rect.Height * 0.25f, rect.Right, rect.Top + rect.Height * 0.25f);
        g.DrawLine(pen, rect.Left, rect.Top + rect.Height * 0.50f, rect.Right - rect.Width * 0.15f, rect.Top + rect.Height * 0.50f);
        g.DrawLine(pen, rect.Left, rect.Top + rect.Height * 0.75f, rect.Right - rect.Width * 0.30f, rect.Top + rect.Height * 0.75f);
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(from.A + (to.A - from.A) * amount),
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }

    private static GraphicsPath RoundedRectF(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2f;
        var arc = new RectangleF(rect.Location, new SizeF(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
