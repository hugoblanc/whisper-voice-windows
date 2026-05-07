using System.Drawing.Drawing2D;
using WhisperVoice.Api;
using WhisperVoice.History;
using WhisperVoice.Logging;
using WhisperVoice.Processing;

namespace WhisperVoice.Config;

public class PreferencesWindow : Form
{
    private static readonly Color GlassTopColor = Color.FromArgb(117, 190, 211);
    private static readonly Color GlassBottomColor = Color.FromArgb(95, 143, 179);
    private static readonly Color SurfaceColor = Color.FromArgb(226, 246, 252);
    private static readonly Color SurfaceAltColor = Color.FromArgb(211, 237, 246);
    private static readonly Color FieldColor = Color.FromArgb(242, 251, 254);
    private static readonly Color PrimaryTextColor = Color.FromArgb(35, 58, 74);
    private static readonly Color MutedTextColor = Color.FromArgb(87, 119, 135);
    private static readonly Color AccentColor = Color.FromArgb(255, 92, 53);
    private static readonly Color AccentHoverColor = Color.FromArgb(255, 116, 74);
    private static readonly Color BorderColor = Color.FromArgb(148, 202, 219);

    private FlowLayoutPanel _navPanel = null!;
    private Panel _contentPanel = null!;
    private readonly List<PreferenceSection> _sections = new();
    private string _activeSection = "General";

    // General tab
    private HudDropdown _providerCombo = null!;
    private TextBox _apiKeyTextBox = null!;
    private LinkLabel _apiKeyLink = null!;
    private Button _testConnectionButton = null!;
    private Label _connectionStatusLabel = null!;
    private HudDropdown _audioCaptureModeCombo = null!;
    private Label _audioCaptureModeDescriptionLabel = null!;
    private HudDropdown _processingModelCombo = null!;
    private TextBox _customVocabularyTextBox = null!;

    // Shortcuts tab
    private HudDropdown _shortcutCombo = null!;
    private HudDropdown _pttCombo = null!;

    // Modes tab
    private ListBox _builtInModesList = null!;
    private ListBox _customModesList = null!;
    private Button _editModeButton = null!;
    private Button _deleteModeButton = null!;
    private readonly List<CustomModeConfig> _customModes = new();

    // Auto mode tab
    private CheckBox _autoModeEnabledCheckBox = null!;
    private CheckBox _autoModeFallbackCheckBox = null!;
    private DataGridView _autoModeRulesGrid = null!;
    private Button _editAutoModeRuleButton = null!;
    private Button _deleteAutoModeRuleButton = null!;
    private readonly List<AutoModeRuleConfig> _autoModeRules = new();

    // Actions tab
    private ListBox _postActionsList = null!;
    private Button _setActivePostActionButton = null!;
    private Button _editPostActionButton = null!;
    private Button _deletePostActionButton = null!;
    private readonly List<PostActionConfig> _postActions = new();
    private string _activePostActionId = PostActionConfig.BuiltInPasteId;
    private CheckBox _autoPostActionEnabledCheckBox = null!;
    private DataGridView _autoPostActionRulesGrid = null!;
    private Button _editAutoPostActionRuleButton = null!;
    private Button _deleteAutoPostActionRuleButton = null!;
    private readonly List<AutoPostActionRuleConfig> _autoPostActionRules = new();

    // Projects tab
    private CheckBox _projectTaggingEnabledCheckBox = null!;
    private ListBox _projectsList = null!;
    private Button _renameProjectButton = null!;
    private Button _archiveProjectButton = null!;
    private readonly List<ProjectConfig> _projects = new();

    // Logs tab
    private TextBox _logTextBox = null!;
    private CheckBox _autoScrollCheckBox = null!;
    private System.Windows.Forms.Timer _logRefreshTimer = null!;

    // Journal tab
    private DataGridView _journalGrid = null!;
    private TextBox _journalDetailsTextBox = null!;

    // Footer
    private Button _saveButton = null!;
    private Button _cancelButton = null!;
    private FlowLayoutPanel _footerPanel = null!;

    private readonly AppConfig _originalConfig;
    private bool _connectionSuccessful;

    public event Action<AppConfig>? SettingsSaved;

    public PreferencesWindow(AppConfig config)
    {
        _originalConfig = config;
        InitializeComponents();
        LoadCurrentSettings();
        StartLogRefreshTimer();
    }

    private void InitializeComponents()
    {
        Text = "Whisper Voice - Preferences";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(960, 720);
        MinimumSize = new Size(900, 640);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = GlassBottomColor;
        DoubleBuffered = true;

        _navPanel = new FlowLayoutPanel
        {
            Location = new Point(24, 18),
            Size = new Size(ClientSize.Width - 48, 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = GlassTopColor,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        _contentPanel = new Panel
        {
            Location = new Point(24, 76),
            Size = new Size(ClientSize.Width - 48, ClientSize.Height - 148),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = SurfaceColor,
            Padding = new Padding(16)
        };

        // Create tabs
        var generalTab = CreateGeneralTab();
        var shortcutsTab = CreateShortcutsTab();
        var modesTab = CreateModesTab();
        var autoModeTab = CreateAutoModeTab();
        var actionsTab = CreateActionsTab();
        var projectsTab = CreateProjectsTab();
        var journalTab = CreateJournalTab();
        var logsTab = CreateLogsTab();

        AddPreferenceSection("General", generalTab);
        AddPreferenceSection("Shortcuts", shortcutsTab);
        AddPreferenceSection("Modes", modesTab);
        AddPreferenceSection("Auto Mode", autoModeTab);
        AddPreferenceSection("Actions", actionsTab);
        AddPreferenceSection("Projects", projectsTab);
        AddPreferenceSection("Journal", journalTab);
        AddPreferenceSection("Logs", logsTab);

        _footerPanel = new FlowLayoutPanel
        {
            Height = 60,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 14, 10),
            BackColor = GlassBottomColor
        };

        _saveButton = new Button
        {
            Text = "Save",
            Size = new Size(118, 40),
            FlatStyle = FlatStyle.Flat
        };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(118, 40),
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.Click += (_, _) => Close();

        _footerPanel.Controls.Add(_cancelButton);
        _footerPanel.Controls.Add(_saveButton);

        Controls.Add(_navPanel);
        Controls.Add(_contentPanel);
        Controls.Add(_footerPanel);

        ApplyPreferencesTheme();
        SelectPreferenceSection("General");
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(ClientRectangle, GlassTopColor, GlassBottomColor, LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    private void AddPreferenceSection(string title, Panel page)
    {
        page.Text = title;
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        page.Margin = Padding.Empty;

        var navButton = new PreferenceNavButton(title)
        {
            Width = title switch
            {
                "Auto Mode" => 128,
                "Shortcuts" => 118,
                _ => 108
            },
            Height = 42,
            Margin = new Padding(0, 0, 6, 0)
        };
        navButton.Click += (_, _) => SelectPreferenceSection(title);

        _sections.Add(new PreferenceSection(title, page, navButton));
        _navPanel.Controls.Add(navButton);
        _contentPanel.Controls.Add(page);
    }

    private void SelectPreferenceSection(string title)
    {
        _activeSection = title;

        foreach (var section in _sections)
        {
            var selected = string.Equals(section.Title, title, StringComparison.OrdinalIgnoreCase);
            section.Page.Visible = selected;
            section.Button.Selected = selected;
            if (selected)
            {
                section.Page.BringToFront();
            }
        }
    }

    private void ApplyPreferencesTheme()
    {
        _contentPanel.BackColor = SurfaceColor;

        foreach (var section in _sections)
        {
            section.Page.BackColor = SurfaceColor;
            section.Page.ForeColor = PrimaryTextColor;
            ApplyControlTheme(section.Page);
        }

        foreach (Control control in _footerPanel.Controls)
        {
            ApplyControlTheme(control);
        }

        StyleButton(_saveButton, primary: true);
        StyleButton(_cancelButton, primary: false);
    }

    private void ApplyControlTheme(Control control)
    {
        switch (control)
        {
            case TableLayoutPanel table:
                table.BackColor = SurfaceColor;
                table.ForeColor = PrimaryTextColor;
                break;
            case FlowLayoutPanel flow:
                flow.BackColor = SurfaceColor;
                flow.ForeColor = PrimaryTextColor;
                break;
            case Panel panel:
                panel.BackColor = SurfaceColor;
                panel.ForeColor = PrimaryTextColor;
                break;
            case LinkLabel link:
                link.BackColor = SurfaceColor;
                link.LinkColor = AccentColor;
                link.ActiveLinkColor = AccentHoverColor;
                link.VisitedLinkColor = AccentColor;
                break;
            case Label label:
                label.BackColor = SurfaceColor;
                label.Font = new Font("Segoe UI", label.Font.Style == FontStyle.Bold ? 10.5f : 10f, label.Font.Style);
                if (label.ForeColor == Color.Gray || label.ForeColor == SystemColors.ControlText || label.ForeColor == Color.Empty)
                {
                    label.ForeColor = label.Text.Length > 70 ? MutedTextColor : PrimaryTextColor;
                }
                break;
            case Button button:
                StyleButton(button, primary: button == _saveButton);
                break;
            case TextBox textBox:
                StyleTextBox(textBox);
                break;
            case HudDropdown dropdown:
                StyleDropdown(dropdown);
                break;
            case ListBox listBox:
                StyleListBox(listBox);
                break;
            case DataGridView grid:
                StyleGrid(grid);
                break;
            case CheckBox checkBox:
                checkBox.BackColor = SurfaceColor;
                checkBox.ForeColor = PrimaryTextColor;
                checkBox.Font = new Font("Segoe UI", 10f);
                checkBox.FlatStyle = FlatStyle.Flat;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyControlTheme(child);
        }
    }

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BackColor = FieldColor;
        textBox.ForeColor = PrimaryTextColor;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = new Font("Segoe UI", textBox.Multiline ? 10.5f : 10f);
        if (!textBox.Multiline && textBox.Height < 32)
        {
            textBox.AutoSize = false;
            textBox.Height = 32;
        }
    }

    private static void StyleDropdown(HudDropdown dropdown)
    {
        dropdown.BackColor = FieldColor;
        dropdown.ForeColor = PrimaryTextColor;
        dropdown.Font = new Font("Segoe UI", 10f);
        if (dropdown.Height < 38)
        {
            dropdown.Height = 38;
        }
    }

    private static void StyleListBox(ListBox listBox)
    {
        listBox.BackColor = FieldColor;
        listBox.ForeColor = PrimaryTextColor;
        listBox.BorderStyle = BorderStyle.FixedSingle;
        listBox.Font = new Font("Segoe UI", 10f);
        listBox.ItemHeight = Math.Max(listBox.ItemHeight, 26);
        listBox.DrawMode = DrawMode.OwnerDrawFixed;
        listBox.DrawItem -= DrawListBoxItem;
        listBox.DrawItem += DrawListBoxItem;
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = SurfaceColor;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = BorderColor;
        grid.EnableHeadersVisualStyles = false;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAltColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryTextColor;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersHeight = 34;
        grid.DefaultCellStyle.BackColor = FieldColor;
        grid.DefaultCellStyle.ForeColor = PrimaryTextColor;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(42, 138, 206);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Padding = new Padding(8, 2, 8, 2);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(232, 247, 252);
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 30);
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.BackColor = primary ? AccentColor : Color.FromArgb(197, 230, 240);
        button.ForeColor = primary ? Color.White : PrimaryTextColor;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? AccentHoverColor : Color.FromArgb(148, 216, 232);
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHoverColor : Color.FromArgb(215, 242, 249);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(226, 74, 37) : Color.FromArgb(178, 218, 232);
        button.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        if (button.Width < 112) button.Width = 112;
        if (button.Height < 38) button.Height = 38;
        button.Resize -= RoundButtonOnResize;
        button.Resize += RoundButtonOnResize;
        ApplyRoundedRegion(button);
    }

    private static void RoundButtonOnResize(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            ApplyRoundedRegion(button);
        }
    }

    private static void DrawListBoxItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count) return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var back = selected ? Color.FromArgb(58, 147, 190) : listBox.BackColor;
        var fore = selected ? Color.White : PrimaryTextColor;

        using (var fill = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        var textBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 16), e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            listBox.Items[e.Index]?.ToString() ?? "",
            listBox.Font,
            textBounds,
            fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static void ApplyRoundedRegion(Button button)
    {
        if (button.Width <= 0 || button.Height <= 0) return;
        button.Region?.Dispose();
        button.Region = new Region(RoundedRect(new Rectangle(0, 0, button.Width, button.Height), Math.Max(10, button.Height / 2)));
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private Panel CreateGeneralTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);
        tab.AutoScroll = true;

        // Provider selection
        var providerLabel = new Label
        {
            Text = "Transcription Provider:",
            Location = new Point(15, 20),
            AutoSize = true
        };

        _providerCombo = new HudDropdown
        {
            Location = new Point(15, 48),
            Size = new Size(300, 38)
        };

        foreach (var provider in TranscriptionProviderFactory.GetAvailableProviders())
        {
            _providerCombo.Items.Add(new ProviderComboItem(provider));
        }
        _providerCombo.SelectedIndexChanged += ProviderCombo_Changed;

        // API Key
        var apiKeyLabel = new Label
        {
            Text = "API Key:",
            Location = new Point(15, 94),
            AutoSize = true
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(15, 122),
            Size = new Size(680, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            UseSystemPasswordChar = true,
            PlaceholderText = "sk-..."
        };
        _apiKeyTextBox.TextChanged += (_, _) => ResetConnectionStatus();

        _apiKeyLink = new LinkLabel
        {
            Text = "Get your API key from platform.openai.com",
            Location = new Point(15, 156),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _apiKeyLink.Click += ApiKeyLink_Click;

        // Test Connection
        _testConnectionButton = new Button
        {
            Text = "Test Connection",
            Location = new Point(15, 190),
            Size = new Size(148, 38),
            FlatStyle = FlatStyle.Flat
        };
        _testConnectionButton.Click += TestConnectionButton_Click;

        _connectionStatusLabel = new Label
        {
            Text = "",
            Location = new Point(176, 198),
            Size = new Size(520, 24),
            AutoSize = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var audioCaptureLabel = new Label
        {
            Text = "Recording latency mode:",
            Location = new Point(15, 254),
            AutoSize = true
        };

        _audioCaptureModeCombo = new HudDropdown
        {
            Location = new Point(15, 282),
            Size = new Size(320, 38)
        };
        _audioCaptureModeCombo.Items.AddRange(new object[]
        {
            new AudioCaptureModeComboItem(AudioCaptureMode.Instant, "Instant"),
            new AudioCaptureModeComboItem(AudioCaptureMode.Balanced, "Balanced"),
            new AudioCaptureModeComboItem(AudioCaptureMode.Privacy, "Privacy")
        });
        _audioCaptureModeCombo.SelectedIndexChanged += (_, _) => UpdateAudioCaptureModeDescription();

        _audioCaptureModeDescriptionLabel = new Label
        {
            Text = "",
            Location = new Point(15, 328),
            Size = new Size(680, 58),
            AutoSize = false,
            ForeColor = Color.Gray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var processingModelLabel = new Label
        {
            Text = "AI processing model:",
            Location = new Point(15, 414),
            AutoSize = true
        };

        _processingModelCombo = new HudDropdown
        {
            Location = new Point(15, 442),
            Size = new Size(470, 38)
        };
        foreach (var model in ProcessingModelCatalog.GetAvailableModels())
        {
            _processingModelCombo.Items.Add(new ProcessingModelComboItem(model));
        }

        var processingModelHint = new Label
        {
            Text = "Used only for Clean/Formel/Casual/Markdown/Super and custom modes. Brut transcription does not call the LLM.",
            Location = new Point(15, 488),
            Size = new Size(680, 36),
            AutoSize = false,
            ForeColor = Color.Gray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var vocabularyLabel = new Label
        {
            Text = "Custom vocabulary:",
            Location = new Point(15, 540),
            AutoSize = true
        };

        _customVocabularyTextBox = new TextBox
        {
            Location = new Point(15, 568),
            Size = new Size(680, 82),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "PostHog, Kubernetes, Chatwoot, project names..."
        };

        var vocabularyHint = new Label
        {
            Text = "Comma-separated terms sent to transcription as a prompt, then preserved during AI processing.",
            Location = new Point(15, 660),
            Size = new Size(680, 22),
            AutoSize = false,
            ForeColor = Color.Gray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        tab.Controls.AddRange(new Control[]
        {
            providerLabel, _providerCombo,
            apiKeyLabel, _apiKeyTextBox, _apiKeyLink,
            _testConnectionButton, _connectionStatusLabel,
            audioCaptureLabel, _audioCaptureModeCombo, _audioCaptureModeDescriptionLabel,
            processingModelLabel, _processingModelCombo, processingModelHint,
            vocabularyLabel, _customVocabularyTextBox, vocabularyHint
        });

        return tab;
    }

    private Panel CreateShortcutsTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);

        // Toggle shortcut
        var shortcutLabel = new Label
        {
            Text = "Toggle Shortcut (start/stop recording):",
            Location = new Point(15, 20),
            AutoSize = true
        };

        _shortcutCombo = new HudDropdown
        {
            Location = new Point(15, 50),
            Size = new Size(360, 38)
        };
        _shortcutCombo.Items.AddRange(new object[]
        {
            "Ctrl+Shift+Space (recommended)",
            "Alt+Space",
            "Ctrl+Space",
            "Win+Shift+Space"
        });

        // PTT key
        var pttLabel = new Label
        {
            Text = "Push-to-Talk Key (hold to record):",
            Location = new Point(15, 108),
            AutoSize = true
        };

        _pttCombo = new HudDropdown
        {
            Location = new Point(15, 138),
            Size = new Size(280, 38)
        };
        _pttCombo.Items.AddRange(new object[]
        {
            "F1", "F2", "F3 (recommended)", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
        });

        // Note
        var noteLabel = new Label
        {
            Text = "Note: Changes take effect immediately after saving.",
            ForeColor = Color.Gray,
            Location = new Point(15, 204),
            AutoSize = true
        };

        tab.Controls.AddRange(new Control[]
        {
            shortcutLabel, _shortcutCombo,
            pttLabel, _pttCombo,
            noteLabel
        });

        return tab;
    }

    private Panel CreateModesTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var builtInLabel = new Label
        {
            Text = "Built-in modes",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        _builtInModesList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        foreach (var mode in AIMode.BuiltInModes)
        {
            _builtInModesList.Items.Add(new BuiltInModeListItem(mode));
        }

        var customLabel = new Label
        {
            Text = "Custom modes",
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 6)
        };

        _customModesList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        _customModesList.SelectedIndexChanged += (_, _) => UpdateModeButtons();
        _customModesList.DoubleClick += (_, _) => EditSelectedMode();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 4),
            Margin = Padding.Empty
        };

        var addModeButton = new Button
        {
            Text = "Add...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat
        };
        addModeButton.Click += AddModeButton_Click;

        _editModeButton = new Button
        {
            Text = "Edit...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editModeButton.Click += (_, _) => EditSelectedMode();

        _deleteModeButton = new Button
        {
            Text = "Delete",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _deleteModeButton.Click += DeleteModeButton_Click;

        buttonPanel.Controls.AddRange(new Control[] { addModeButton, _editModeButton, _deleteModeButton });

        var hintLabel = new Label
        {
            Text = "Tab cycles through enabled modes while recording.",
            ForeColor = Color.Gray,
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        root.Controls.Add(builtInLabel, 0, 0);
        root.Controls.Add(_builtInModesList, 0, 1);
        root.Controls.Add(customLabel, 0, 2);
        root.Controls.Add(_customModesList, 0, 3);
        root.Controls.Add(buttonPanel, 0, 4);
        root.Controls.Add(hintLabel, 0, 5);

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateAutoModeTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        _autoModeEnabledCheckBox = new CheckBox
        {
            Text = "Automatically select mode from the active app or window title",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        _autoModeEnabledCheckBox.CheckedChanged += (_, _) =>
        {
            _autoModeFallbackCheckBox.Enabled = _autoModeEnabledCheckBox.Checked;
        };

        _autoModeFallbackCheckBox = new CheckBox
        {
            Text = "Keep the last-used mode when no rule matches",
            AutoSize = true,
            Margin = new Padding(18, 0, 0, 10)
        };

        _autoModeRulesGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Margin = new Padding(0, 0, 0, 8)
        };
        _autoModeRulesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 36 });
        _autoModeRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Rule", FillWeight = 140 });
        _autoModeRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "App process", FillWeight = 90 });
        _autoModeRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title contains", FillWeight = 130 });
        _autoModeRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode", FillWeight = 90 });
        _autoModeRulesGrid.SelectionChanged += (_, _) => UpdateAutoModeRuleButtons();
        _autoModeRulesGrid.DoubleClick += (_, _) => EditSelectedAutoModeRule();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 4),
            Margin = Padding.Empty
        };

        var addRuleButton = new Button
        {
            Text = "Add...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat
        };
        addRuleButton.Click += AddAutoModeRuleButton_Click;

        _editAutoModeRuleButton = new Button
        {
            Text = "Edit...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editAutoModeRuleButton.Click += (_, _) => EditSelectedAutoModeRule();

        _deleteAutoModeRuleButton = new Button
        {
            Text = "Delete",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _deleteAutoModeRuleButton.Click += DeleteAutoModeRuleButton_Click;

        buttonPanel.Controls.AddRange(new Control[] { addRuleButton, _editAutoModeRuleButton, _deleteAutoModeRuleButton });

        var hintLabel = new Label
        {
            Text = "Rules are evaluated from the most specific match first. A Tab mode switch only affects the current recording.",
            ForeColor = Color.Gray,
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        root.Controls.Add(_autoModeEnabledCheckBox, 0, 0);
        root.Controls.Add(_autoModeFallbackCheckBox, 0, 1);
        root.Controls.Add(_autoModeRulesGrid, 0, 2);
        root.Controls.Add(buttonPanel, 0, 3);
        root.Controls.Add(hintLabel, 0, 4);

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateActionsTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

        var headingLabel = new Label
        {
            Text = "Post-transcription actions",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _postActionsList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        _postActionsList.SelectedIndexChanged += (_, _) => UpdatePostActionButtons();
        _postActionsList.DoubleClick += (_, _) => EditSelectedPostAction();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 4),
            Margin = Padding.Empty
        };

        _setActivePostActionButton = new Button
        {
            Text = "Set Active",
            Size = new Size(112, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _setActivePostActionButton.Click += (_, _) => SetSelectedPostActionActive();

        var addActionButton = new Button
        {
            Text = "Add...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat
        };
        addActionButton.Click += AddPostActionButton_Click;

        _editPostActionButton = new Button
        {
            Text = "Edit...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editPostActionButton.Click += (_, _) => EditSelectedPostAction();

        _deletePostActionButton = new Button
        {
            Text = "Delete",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _deletePostActionButton.Click += DeletePostActionButton_Click;

        buttonPanel.Controls.AddRange(new Control[]
        {
            _setActivePostActionButton,
            addActionButton,
            _editPostActionButton,
            _deletePostActionButton
        });

        _autoPostActionEnabledCheckBox = new CheckBox
        {
            Text = "Automatically select action from the active app or window title",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 8)
        };
        _autoPostActionEnabledCheckBox.CheckedChanged += (_, _) =>
        {
            _autoPostActionRulesGrid.Enabled = _autoPostActionEnabledCheckBox.Checked;
            UpdateAutoPostActionRuleButtons();
        };

        _autoPostActionRulesGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Margin = new Padding(0, 0, 0, 8)
        };
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 36 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Rule", FillWeight = 140 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "App process", FillWeight = 90 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title contains", FillWeight = 130 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action", FillWeight = 100 });
        _autoPostActionRulesGrid.SelectionChanged += (_, _) => UpdateAutoPostActionRuleButtons();
        _autoPostActionRulesGrid.DoubleClick += (_, _) => EditSelectedAutoPostActionRule();

        var autoButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 4),
            Margin = Padding.Empty
        };

        var addAutoRuleButton = new Button
        {
            Text = "Add...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat
        };
        addAutoRuleButton.Click += AddAutoPostActionRuleButton_Click;

        _editAutoPostActionRuleButton = new Button
        {
            Text = "Edit...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editAutoPostActionRuleButton.Click += (_, _) => EditSelectedAutoPostActionRule();

        _deleteAutoPostActionRuleButton = new Button
        {
            Text = "Delete",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _deleteAutoPostActionRuleButton.Click += DeleteAutoPostActionRuleButton_Click;

        autoButtonPanel.Controls.AddRange(new Control[] { addAutoRuleButton, _editAutoPostActionRuleButton, _deleteAutoPostActionRuleButton });

        var variablesTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false,
            Text = string.Join(Environment.NewLine, new[]
            {
                "Commands receive environment variables:",
                "WV_TRANSCRIPTION, WV_RAW_TRANSCRIPTION, WV_APP_PROCESS, WV_APP_WINDOW_TITLE, WV_BROWSER_URL, WV_BROWSER_HOST, WV_WORKSPACE, WV_PROJECT, WV_MODE, WV_PROVIDER",
                "Default action is Paste. Auto-action rules override the active action only when enabled and matched."
            }),
            Margin = Padding.Empty
        };

        root.Controls.Add(headingLabel, 0, 0);
        root.Controls.Add(_postActionsList, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        root.Controls.Add(_autoPostActionEnabledCheckBox, 0, 3);
        root.Controls.Add(_autoPostActionRulesGrid, 0, 4);
        root.Controls.Add(autoButtonPanel, 0, 5);
        root.Controls.Add(variablesTextBox, 0, 6);

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateLogsTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        _autoScrollCheckBox = new CheckBox
        {
            Text = "Auto-scroll",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Checked = true,
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 8.5F),
            WordWrap = false,
            Margin = new Padding(0, 0, 0, 8)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };

        var clearLogsButton = new Button
        {
            Text = "Clear",
            Size = new Size(96, 30),
            FlatStyle = FlatStyle.Flat
        };
        clearLogsButton.Click += ClearLogsButton_Click;

        var openLogFolderButton = new Button
        {
            Text = "Open Folder",
            Size = new Size(112, 30),
            FlatStyle = FlatStyle.Flat
        };
        openLogFolderButton.Click += (_, _) => Logger.OpenLogFolder();

        buttonPanel.Controls.AddRange(new Control[] { clearLogsButton, openLogFolderButton });

        root.Controls.Add(_autoScrollCheckBox, 0, 0);
        root.Controls.Add(_logTextBox, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);

        tab.Controls.Add(root);

        return tab;
    }

    private Panel CreateProjectsTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        _projectTaggingEnabledCheckBox = new CheckBox
        {
            Text = "Predict and save projects for recordings",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        _projectsList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        _projectsList.SelectedIndexChanged += (_, _) => UpdateProjectButtons();
        _projectsList.DoubleClick += (_, _) => RenameSelectedProject();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 4),
            Margin = Padding.Empty
        };

        var addProjectButton = new Button
        {
            Text = "Add...",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat
        };
        addProjectButton.Click += AddProjectButton_Click;

        _renameProjectButton = new Button
        {
            Text = "Rename...",
            Size = new Size(112, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _renameProjectButton.Click += (_, _) => RenameSelectedProject();

        _archiveProjectButton = new Button
        {
            Text = "Archive",
            Size = new Size(104, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _archiveProjectButton.Click += ArchiveProjectButton_Click;

        buttonPanel.Controls.AddRange(new Control[] { addProjectButton, _renameProjectButton, _archiveProjectButton });

        var hintLabel = new Label
        {
            Text = "Archived projects stay in history but are no longer suggested during recording.",
            ForeColor = Color.Gray,
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };

        root.Controls.Add(_projectTaggingEnabledCheckBox, 0, 0);
        root.Controls.Add(_projectsList, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        root.Controls.Add(hintLabel, 0, 3);

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateJournalTab()
    {
        var tab = new Panel();
        tab.Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _journalGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Margin = new Padding(0, 0, 0, 8)
        };
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time", FillWeight = 90 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 70 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode", FillWeight = 85 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Provider", HeaderText = "Provider", FillWeight = 90 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total", FillWeight = 65 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recording", HeaderText = "Rec", FillWeight = 65 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Transcribe", HeaderText = "Trans", FillWeight = 65 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Processing", HeaderText = "AI", FillWeight = 65 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action", FillWeight = 65 });
        _journalGrid.SelectionChanged += (_, _) => UpdateJournalDetails();

        _journalDetailsTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 8.5F),
            Margin = new Padding(0, 0, 0, 8)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };

        var refreshButton = new Button
        {
            Text = "Refresh",
            Size = new Size(96, 30),
            FlatStyle = FlatStyle.Flat
        };
        refreshButton.Click += (_, _) => RefreshJournal();

        var openFolderButton = new Button
        {
            Text = "Open Folder",
            Size = new Size(112, 30),
            FlatStyle = FlatStyle.Flat
        };
        openFolderButton.Click += (_, _) => RecordingJournal.OpenFolder();

        var clearButton = new Button
        {
            Text = "Clear",
            Size = new Size(96, 30),
            FlatStyle = FlatStyle.Flat
        };
        clearButton.Click += ClearJournalButton_Click;

        buttonPanel.Controls.AddRange(new Control[] { refreshButton, openFolderButton, clearButton });

        root.Controls.Add(_journalGrid, 0, 0);
        root.Controls.Add(_journalDetailsTextBox, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);

        tab.Controls.Add(root);
        return tab;
    }

    private void AddModeButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new CustomModeEditorDialog(null, GetExistingModeIds());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _customModes.Add(dialog.Mode);
        RefreshCustomModesList();
        _customModesList.SelectedIndex = _customModes.Count - 1;
    }

    private void EditSelectedMode()
    {
        if (_customModesList.SelectedItem is not CustomModeListItem item) return;

        var existingIds = GetExistingModeIds()
            .Where(id => !string.Equals(id, item.Mode.Id, StringComparison.OrdinalIgnoreCase));

        using var dialog = new CustomModeEditorDialog(item.Mode, existingIds);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _customModes[item.Index] = dialog.Mode;
        RefreshCustomModesList();
        _customModesList.SelectedIndex = item.Index;
    }

    private void DeleteModeButton_Click(object? sender, EventArgs e)
    {
        if (_customModesList.SelectedItem is not CustomModeListItem item) return;

        var result = MessageBox.Show(
            $"Delete custom mode '{item.Mode.Name}'?",
            "Delete Custom Mode",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        _customModes.RemoveAt(item.Index);
        RefreshCustomModesList();
    }

    private void AddAutoModeRuleButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new AutoModeRuleEditorDialog(null, GetAvailableModesForAutoRules());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _autoModeRules.Add(dialog.Rule);
        RefreshAutoModeRulesGrid();
        if (_autoModeRulesGrid.Rows.Count > 0)
        {
            var lastRow = _autoModeRulesGrid.Rows[_autoModeRulesGrid.Rows.Count - 1];
            lastRow.Selected = true;
            _autoModeRulesGrid.CurrentCell = lastRow.Cells[0];
        }
    }

    private void EditSelectedAutoModeRule()
    {
        if (_autoModeRulesGrid.CurrentRow?.Tag is not AutoModeRuleListItem item) return;

        using var dialog = new AutoModeRuleEditorDialog(item.Rule, GetAvailableModesForAutoRules());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _autoModeRules[item.Index] = dialog.Rule;
        RefreshAutoModeRulesGrid();
        if (item.Index >= 0 && item.Index < _autoModeRulesGrid.Rows.Count)
        {
            _autoModeRulesGrid.Rows[item.Index].Selected = true;
            _autoModeRulesGrid.CurrentCell = _autoModeRulesGrid.Rows[item.Index].Cells[0];
        }
    }

    private void DeleteAutoModeRuleButton_Click(object? sender, EventArgs e)
    {
        if (_autoModeRulesGrid.CurrentRow?.Tag is not AutoModeRuleListItem item) return;

        var result = MessageBox.Show(
            $"Delete auto mode rule '{item.Rule.Name}'?",
            "Delete Auto Mode Rule",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        _autoModeRules.RemoveAt(item.Index);
        RefreshAutoModeRulesGrid();
    }

    private void AddPostActionButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new PostActionEditorDialog(null, GetExistingPostActionIds());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _postActions.Add(dialog.Action);
        RefreshPostActionsList();
        _postActionsList.SelectedIndex = _postActions.Count - 1;
    }

    private void EditSelectedPostAction()
    {
        if (_postActionsList.SelectedItem is not PostActionListItem item || item.Action.IsBuiltIn) return;

        var existingIds = GetExistingPostActionIds()
            .Where(id => !string.Equals(id, item.Action.Id, StringComparison.OrdinalIgnoreCase));

        using var dialog = new PostActionEditorDialog(item.Action, existingIds);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _postActions[item.Index] = dialog.Action;
        RefreshPostActionsList();
        _postActionsList.SelectedIndex = item.Index;
    }

    private void DeletePostActionButton_Click(object? sender, EventArgs e)
    {
        if (_postActionsList.SelectedItem is not PostActionListItem item || item.Action.IsBuiltIn) return;

        var result = MessageBox.Show(
            $"Delete action '{item.Action.Label}'?",
            "Delete Post Action",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        _postActions.RemoveAt(item.Index);
        if (string.Equals(_activePostActionId, item.Action.Id, StringComparison.OrdinalIgnoreCase))
        {
            _activePostActionId = PostActionConfig.BuiltInPasteId;
        }

        RefreshPostActionsList();
    }

    private void SetSelectedPostActionActive()
    {
        if (_postActionsList.SelectedItem is not PostActionListItem item) return;
        _activePostActionId = item.Action.Id;
        RefreshPostActionsList();
        _postActionsList.SelectedIndex = item.Index;
    }

    private void AddAutoPostActionRuleButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new AutoPostActionRuleEditorDialog(null, GetAvailablePostActionsForRules());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _autoPostActionRules.Add(dialog.Rule);
        RefreshAutoPostActionRulesGrid();
        if (_autoPostActionRulesGrid.Rows.Count > 0)
        {
            var lastRow = _autoPostActionRulesGrid.Rows[_autoPostActionRulesGrid.Rows.Count - 1];
            lastRow.Selected = true;
            _autoPostActionRulesGrid.CurrentCell = lastRow.Cells[0];
        }
    }

    private void EditSelectedAutoPostActionRule()
    {
        if (_autoPostActionRulesGrid.CurrentRow?.Tag is not AutoPostActionRuleListItem item) return;

        using var dialog = new AutoPostActionRuleEditorDialog(item.Rule, GetAvailablePostActionsForRules());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _autoPostActionRules[item.Index] = dialog.Rule;
        RefreshAutoPostActionRulesGrid();
        if (item.Index >= 0 && item.Index < _autoPostActionRulesGrid.Rows.Count)
        {
            _autoPostActionRulesGrid.Rows[item.Index].Selected = true;
            _autoPostActionRulesGrid.CurrentCell = _autoPostActionRulesGrid.Rows[item.Index].Cells[0];
        }
    }

    private void DeleteAutoPostActionRuleButton_Click(object? sender, EventArgs e)
    {
        if (_autoPostActionRulesGrid.CurrentRow?.Tag is not AutoPostActionRuleListItem item) return;

        var result = MessageBox.Show(
            $"Delete auto action rule '{item.Rule.Name}'?",
            "Delete Auto Action Rule",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        _autoPostActionRules.RemoveAt(item.Index);
        RefreshAutoPostActionRulesGrid();
    }

    private void AddProjectButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new ProjectEditorDialog(null);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var existing = _projects.FirstOrDefault(project =>
            string.Equals(project.Name, dialog.ProjectName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Archived = false;
        }
        else
        {
            _projects.Add(new ProjectConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = dialog.ProjectName,
                Color = PickProjectColor(_projects.Count),
                CreatedAt = DateTime.Now
            });
        }

        RefreshProjectsList();
    }

    private void RenameSelectedProject()
    {
        if (_projectsList.SelectedItem is not ProjectListItem item) return;

        using var dialog = new ProjectEditorDialog(item.Project.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        item.Project.Name = dialog.ProjectName;
        RefreshProjectsList();
    }

    private void ArchiveProjectButton_Click(object? sender, EventArgs e)
    {
        if (_projectsList.SelectedItem is not ProjectListItem item) return;

        var result = MessageBox.Show(
            $"Archive project '{item.Project.Name}'?",
            "Archive Project",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        item.Project.Archived = true;
        RefreshProjectsList();
    }

    private void RefreshProjectsList()
    {
        if (_projectsList == null) return;

        var previousId = (_projectsList.SelectedItem as ProjectListItem)?.Project.Id;
        _projectsList.Items.Clear();

        var orderedProjects = _projects
            .OrderBy(project => project.Archived)
            .ThenBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var project in orderedProjects)
        {
            _projectsList.Items.Add(new ProjectListItem(project));
        }

        if (!string.IsNullOrWhiteSpace(previousId))
        {
            for (var i = 0; i < _projectsList.Items.Count; i++)
            {
                if (_projectsList.Items[i] is ProjectListItem item &&
                    string.Equals(item.Project.Id, previousId, StringComparison.OrdinalIgnoreCase))
                {
                    _projectsList.SelectedIndex = i;
                    UpdateProjectButtons();
                    return;
                }
            }
        }

        if (_projectsList.Items.Count > 0)
        {
            _projectsList.SelectedIndex = 0;
        }

        UpdateProjectButtons();
    }

    private void UpdateProjectButtons()
    {
        var item = _projectsList?.SelectedItem as ProjectListItem;
        var hasSelection = item != null;
        _renameProjectButton.Enabled = hasSelection;
        _archiveProjectButton.Enabled = hasSelection && item?.Project.Archived == false;
    }

    private static string PickProjectColor(int index)
    {
        string[] colors =
        {
            "#7BC0D6",
            "#FF7A4A",
            "#68D391",
            "#F6C85F",
            "#9F7AEA",
            "#63B3ED",
            "#F687B3"
        };
        return colors[index % colors.Length];
    }

    private void RefreshCustomModesList()
    {
        var previousIndex = _customModesList.SelectedIndex;
        _customModesList.Items.Clear();

        for (var i = 0; i < _customModes.Count; i++)
        {
            _customModesList.Items.Add(new CustomModeListItem(i, _customModes[i]));
        }

        if (_customModesList.Items.Count > 0)
        {
            _customModesList.SelectedIndex = Math.Clamp(previousIndex, 0, _customModesList.Items.Count - 1);
        }

        UpdateModeButtons();
        RefreshAutoModeRulesGrid();
    }

    private void UpdateModeButtons()
    {
        var hasSelection = _customModesList.SelectedItem is CustomModeListItem;
        _editModeButton.Enabled = hasSelection;
        _deleteModeButton.Enabled = hasSelection;
    }

    private IEnumerable<string> GetExistingModeIds() =>
        AIMode.BuiltInModes
            .Select(mode => mode.Id)
            .Concat(_customModes.Select(mode => mode.Id))
            .Where(id => !string.IsNullOrWhiteSpace(id));

    private List<CustomModeConfig> BuildCustomModesForSave()
    {
        var modes = new List<CustomModeConfig>();
        var ids = new HashSet<string>(
            AIMode.BuiltInModes.Select(mode => mode.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var mode in _customModes.Where(mode => mode.IsValid))
        {
            var clone = mode.Clone();
            if (string.IsNullOrWhiteSpace(clone.Id) || ids.Contains(clone.Id))
            {
                clone.Id = CustomModeConfig.CreateUniqueId(clone.Name, ids);
            }

            ids.Add(clone.Id);
            modes.Add(clone);
        }

        return modes;
    }

    private List<AutoModeRuleConfig> BuildAutoModeRulesForSave()
    {
        var rules = _autoModeRules
            .Where(rule => rule.IsValid)
            .Select(rule => rule.Clone())
            .ToList();

        var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            rule.EnsureId(existingIds);
            existingIds.Add(rule.Id);
        }

        return rules;
    }

    private List<AIMode> GetAvailableModesForAutoRules()
    {
        var modes = new List<AIMode>();
        modes.AddRange(AIMode.BuiltInModes);
        modes.AddRange(_customModes.Where(mode => mode.Enabled && mode.IsValid).Select(AIMode.FromCustom));
        return modes;
    }

    private void RefreshAutoModeRulesGrid()
    {
        if (_autoModeRulesGrid == null) return;

        var selectedId = (_autoModeRulesGrid.CurrentRow?.Tag as AutoModeRuleListItem)?.Rule.Id;
        _autoModeRulesGrid.Rows.Clear();

        for (var i = 0; i < _autoModeRules.Count; i++)
        {
            var rule = _autoModeRules[i];
            var rowIndex = _autoModeRulesGrid.Rows.Add(
                rule.Enabled,
                string.IsNullOrWhiteSpace(rule.Name) ? BuildAutoModeRuleName(rule) : rule.Name,
                string.IsNullOrWhiteSpace(rule.ProcessName) ? "*" : rule.ProcessName,
                string.IsNullOrWhiteSpace(rule.WindowTitleContains) ? "*" : rule.WindowTitleContains,
                GetAutoModeRuleModeName(rule.ModeId));

            var row = _autoModeRulesGrid.Rows[rowIndex];
            row.Tag = new AutoModeRuleListItem(i, rule);

            if (!rule.IsValid || GetAvailableModesForAutoRules().All(mode => !string.Equals(mode.Id, rule.ModeId, StringComparison.OrdinalIgnoreCase)))
            {
                row.DefaultCellStyle.ForeColor = Color.DarkOrange;
            }

            if (!string.IsNullOrWhiteSpace(selectedId) && string.Equals(rule.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            {
                row.Selected = true;
                _autoModeRulesGrid.CurrentCell = row.Cells[0];
            }
        }

        UpdateAutoModeRuleButtons();
    }

    private void UpdateAutoModeRuleButtons()
    {
        var hasSelection = _autoModeRulesGrid.CurrentRow?.Tag is AutoModeRuleListItem;
        _editAutoModeRuleButton.Enabled = hasSelection;
        _deleteAutoModeRuleButton.Enabled = hasSelection;
    }

    private void RefreshPostActionsList()
    {
        if (_postActionsList == null) return;

        var previousId = (_postActionsList.SelectedItem as PostActionListItem)?.Action.Id;
        _postActionsList.Items.Clear();

        for (var i = 0; i < _postActions.Count; i++)
        {
            _postActionsList.Items.Add(new PostActionListItem(i, _postActions[i], _activePostActionId));
        }

        if (!string.IsNullOrWhiteSpace(previousId))
        {
            for (var i = 0; i < _postActionsList.Items.Count; i++)
            {
                if (_postActionsList.Items[i] is PostActionListItem item &&
                    string.Equals(item.Action.Id, previousId, StringComparison.OrdinalIgnoreCase))
                {
                    _postActionsList.SelectedIndex = i;
                    break;
                }
            }
        }

        if (_postActionsList.SelectedIndex < 0 && _postActionsList.Items.Count > 0)
        {
            _postActionsList.SelectedIndex = 0;
        }

        UpdatePostActionButtons();
        RefreshAutoPostActionRulesGrid();
    }

    private void UpdatePostActionButtons()
    {
        var item = _postActionsList?.SelectedItem as PostActionListItem;
        var hasSelection = item != null;
        var isCustom = item?.Action.IsBuiltIn == false;
        var isActive = item != null && string.Equals(item.Action.Id, _activePostActionId, StringComparison.OrdinalIgnoreCase);

        _setActivePostActionButton.Enabled = hasSelection && !isActive;
        _editPostActionButton.Enabled = isCustom;
        _deletePostActionButton.Enabled = isCustom;
    }

    private IEnumerable<string> GetExistingPostActionIds() =>
        _postActions
            .Select(action => action.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id));

    private List<PostActionConfig> BuildPostActionsForSave()
    {
        var actions = PostActionConfig.CreateDefaults();
        var ids = new HashSet<string>(
            actions.Select(action => action.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var action in _postActions.Where(action => !action.IsBuiltIn && action.IsValid))
        {
            var clone = action.Clone();
            if (string.IsNullOrWhiteSpace(clone.Id) || ids.Contains(clone.Id))
            {
                clone.Id = PostActionConfig.CreateUniqueId(clone.Label, ids);
            }

            ids.Add(clone.Id);
            actions.Add(clone);
        }

        return actions;
    }

    private List<AutoPostActionRuleConfig> BuildAutoPostActionRulesForSave()
    {
        var rules = _autoPostActionRules
            .Where(rule => rule.IsValid)
            .Select(rule => rule.Clone())
            .ToList();

        var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            rule.EnsureId(existingIds);
            existingIds.Add(rule.Id);
        }

        return rules;
    }

    private List<PostActionConfig> GetAvailablePostActionsForRules() =>
        PostActionConfig.MergeWithBuiltIns(_postActions);

    private void RefreshAutoPostActionRulesGrid()
    {
        if (_autoPostActionRulesGrid == null) return;

        var selectedId = (_autoPostActionRulesGrid.CurrentRow?.Tag as AutoPostActionRuleListItem)?.Rule.Id;
        _autoPostActionRulesGrid.Rows.Clear();

        for (var i = 0; i < _autoPostActionRules.Count; i++)
        {
            var rule = _autoPostActionRules[i];
            var rowIndex = _autoPostActionRulesGrid.Rows.Add(
                rule.Enabled,
                string.IsNullOrWhiteSpace(rule.Name) ? BuildAutoPostActionRuleName(rule) : rule.Name,
                string.IsNullOrWhiteSpace(rule.ProcessName) ? "*" : rule.ProcessName,
                string.IsNullOrWhiteSpace(rule.WindowTitleContains) ? "*" : rule.WindowTitleContains,
                GetPostActionRuleActionName(rule.ActionId));

            var row = _autoPostActionRulesGrid.Rows[rowIndex];
            row.Tag = new AutoPostActionRuleListItem(i, rule);

            if (!rule.IsValid || GetAvailablePostActionsForRules().All(action => !string.Equals(action.Id, rule.ActionId, StringComparison.OrdinalIgnoreCase)))
            {
                row.DefaultCellStyle.ForeColor = Color.DarkOrange;
            }

            if (!string.IsNullOrWhiteSpace(selectedId) && string.Equals(rule.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            {
                row.Selected = true;
                _autoPostActionRulesGrid.CurrentCell = row.Cells[0];
            }
        }

        _autoPostActionRulesGrid.Enabled = _autoPostActionEnabledCheckBox.Checked;
        UpdateAutoPostActionRuleButtons();
    }

    private void UpdateAutoPostActionRuleButtons()
    {
        var hasSelection = _autoPostActionEnabledCheckBox.Checked &&
                           _autoPostActionRulesGrid.CurrentRow?.Tag is AutoPostActionRuleListItem;
        _editAutoPostActionRuleButton.Enabled = hasSelection;
        _deleteAutoPostActionRuleButton.Enabled = hasSelection;
    }

    private string GetPostActionRuleActionName(string actionId)
    {
        var action = GetAvailablePostActionsForRules()
            .FirstOrDefault(action => string.Equals(action.Id, actionId, StringComparison.OrdinalIgnoreCase));
        return action == null ? $"Missing: {actionId}" : action.Label;
    }

    private static string BuildAutoPostActionRuleName(AutoPostActionRuleConfig rule)
    {
        var process = string.IsNullOrWhiteSpace(rule.ProcessName) ? "any app" : rule.ProcessName;
        var title = string.IsNullOrWhiteSpace(rule.WindowTitleContains) ? "" : $" / {rule.WindowTitleContains}";
        return $"{process}{title}";
    }

    private string GetAutoModeRuleModeName(string modeId)
    {
        var mode = GetAvailableModesForAutoRules()
            .FirstOrDefault(mode => string.Equals(mode.Id, modeId, StringComparison.OrdinalIgnoreCase));
        return mode == null ? $"Missing: {modeId}" : mode.Name;
    }

    private static string BuildAutoModeRuleName(AutoModeRuleConfig rule)
    {
        var process = string.IsNullOrWhiteSpace(rule.ProcessName) ? "any app" : rule.ProcessName;
        var title = string.IsNullOrWhiteSpace(rule.WindowTitleContains) ? "" : $" / {rule.WindowTitleContains}";
        return $"{process}{title}";
    }

    private static List<string> ParseVocabulary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(new[] { ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadCurrentSettings()
    {
        // Select current provider
        for (int i = 0; i < _providerCombo.Items.Count; i++)
        {
            if (_providerCombo.Items[i] is ProviderComboItem item &&
                item.Info.Id == _originalConfig.Provider)
            {
                _providerCombo.SelectedIndex = i;
                break;
            }
        }

        // Load API key
        _apiKeyTextBox.Text = _originalConfig.GetCurrentApiKey();

        // Select current shortcut
        _shortcutCombo.SelectedIndex = _originalConfig.ShortcutModifiers switch
        {
            0x0006 => 0, // Ctrl+Shift
            0x0001 => 1, // Alt
            0x0002 => 2, // Ctrl
            0x000C => 3, // Win+Shift
            _ => 0
        };

        // Select current PTT key
        var pttIndex = (_originalConfig.PushToTalkKeyCode - 0x70); // VK_F1 = 0x70
        if (pttIndex >= 0 && pttIndex < _pttCombo.Items.Count)
        {
            _pttCombo.SelectedIndex = (int)pttIndex;
        }
        else
        {
            _pttCombo.SelectedIndex = 2; // F3 default
        }

        for (var i = 0; i < _audioCaptureModeCombo.Items.Count; i++)
        {
            if (_audioCaptureModeCombo.Items[i] is AudioCaptureModeComboItem item &&
                item.Mode == _originalConfig.AudioCaptureMode)
            {
                _audioCaptureModeCombo.SelectedIndex = i;
                break;
            }
        }

        if (_audioCaptureModeCombo.SelectedIndex < 0)
        {
            _audioCaptureModeCombo.SelectedIndex = 0;
        }

        var processingModel = ProcessingModelCatalog.Normalize(_originalConfig.ProcessingModel);
        for (var i = 0; i < _processingModelCombo.Items.Count; i++)
        {
            if (_processingModelCombo.Items[i] is ProcessingModelComboItem item &&
                string.Equals(item.Info.Id, processingModel, StringComparison.OrdinalIgnoreCase))
            {
                _processingModelCombo.SelectedIndex = i;
                break;
            }
        }

        if (_processingModelCombo.SelectedIndex < 0 && _processingModelCombo.Items.Count > 0)
        {
            _processingModelCombo.SelectedIndex = 0;
        }

        _customVocabularyTextBox.Text = string.Join(", ", _originalConfig.CustomVocabulary ?? new List<string>());

        // Load custom modes
        _customModes.Clear();
        _customModes.AddRange((_originalConfig.CustomModes ?? new List<CustomModeConfig>())
            .Select(mode => mode.Clone()));
        RefreshCustomModesList();

        _autoModeEnabledCheckBox.Checked = _originalConfig.AutoModeEnabled;
        _autoModeFallbackCheckBox.Checked = _originalConfig.AutoModeFallbackToLastUsed;
        _autoModeFallbackCheckBox.Enabled = _autoModeEnabledCheckBox.Checked;
        _autoModeRules.Clear();
        _autoModeRules.AddRange((_originalConfig.AutoModeRules ?? AutoModeRuleConfig.CreateDefaults())
            .Select(rule => rule.Clone()));
        RefreshAutoModeRulesGrid();

        _postActions.Clear();
        _postActions.AddRange(PostActionConfig.MergeWithBuiltIns(_originalConfig.PostActions)
            .Select(action => action.Clone()));
        _activePostActionId = PostActionConfig.NormalizeActiveId(_postActions, _originalConfig.ActivePostActionId);
        RefreshPostActionsList();
        _autoPostActionEnabledCheckBox.Checked = _originalConfig.AutoPostActionEnabled;
        _autoPostActionRules.Clear();
        _autoPostActionRules.AddRange((_originalConfig.AutoPostActionRules ?? new List<AutoPostActionRuleConfig>())
            .Select(rule => rule.Clone()));
        RefreshAutoPostActionRulesGrid();

        _projectTaggingEnabledCheckBox.Checked = _originalConfig.ProjectTaggingEnabled;
        _projects.Clear();
        _projects.AddRange(ProjectStore.LoadProjects());
        RefreshProjectsList();

        // Update UI for selected provider
        UpdateProviderUI();

        // Load initial logs
        RefreshLogs();
        RefreshJournal();
    }

    private void UpdateAudioCaptureModeDescription()
    {
        var mode = (_audioCaptureModeCombo.SelectedItem as AudioCaptureModeComboItem)?.Mode ?? AudioCaptureMode.Instant;
        _audioCaptureModeDescriptionLabel.Text = mode switch
        {
            AudioCaptureMode.Instant => "Keeps the microphone ready in the background for near-zero start latency. No audio is sent until a recording is stopped.",
            AudioCaptureMode.Balanced => "Opens the microphone on first use, then keeps it ready for 3 minutes after each recording before releasing it.",
            AudioCaptureMode.Privacy => "Only opens the microphone during active recordings. Start latency depends on Windows and the selected device.",
            _ => ""
        };
    }

    private void ProviderCombo_Changed(object? sender, EventArgs e)
    {
        UpdateProviderUI();
        ResetConnectionStatus();
    }

    private void UpdateProviderUI()
    {
        if (_providerCombo.SelectedItem is ProviderComboItem item)
        {
            // Local provider doesn't need API key
            if (string.IsNullOrEmpty(item.Info.ApiKeyHelpUrl))
            {
                _apiKeyLink.Text = "No API key needed (offline mode)";
                _apiKeyLink.Tag = null;
                _apiKeyLink.Enabled = false;
                _apiKeyTextBox.Enabled = false;
                _apiKeyTextBox.PlaceholderText = "Not required for local mode";
                _testConnectionButton.Text = "Check Model";
            }
            else
            {
                var host = new Uri(item.Info.ApiKeyHelpUrl).Host;
                _apiKeyLink.Text = $"Get your API key from {host}";
                _apiKeyLink.Tag = item.Info.ApiKeyHelpUrl;
                _apiKeyLink.Enabled = true;
                _apiKeyTextBox.Enabled = true;
                _apiKeyTextBox.PlaceholderText = item.Info.Id == "openai" ? "sk-..." : "Enter API key";
                _testConnectionButton.Text = "Test Connection";
            }
        }
    }

    private void ApiKeyLink_Click(object? sender, EventArgs e)
    {
        if (_apiKeyLink.Tag is string url)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }

    private void ResetConnectionStatus()
    {
        _connectionSuccessful = false;
        _connectionStatusLabel.Text = "";
        _connectionStatusLabel.ForeColor = SystemColors.ControlText;
    }

    private async void TestConnectionButton_Click(object? sender, EventArgs e)
    {
        var apiKey = _apiKeyTextBox.Text.Trim();
        var selectedProvider = (_providerCombo.SelectedItem as ProviderComboItem)?.Info;

        if (selectedProvider == null)
        {
            _connectionStatusLabel.Text = "Please select a provider.";
            _connectionStatusLabel.ForeColor = Color.Red;
            return;
        }

        // Validate format first
        if (!TranscriptionProviderFactory.ValidateApiKey(selectedProvider.Id, apiKey, out var formatError))
        {
            _connectionStatusLabel.Text = formatError ?? "Invalid API key format.";
            _connectionStatusLabel.ForeColor = Color.Red;
            return;
        }

        // Disable button during test
        _testConnectionButton.Enabled = false;
        _connectionStatusLabel.Text = "Testing...";
        _connectionStatusLabel.ForeColor = Color.Gray;

        try
        {
            // Create a temporary provider with the entered API key to test
            var tempProvider = TranscriptionProviderFactory.Create(selectedProvider.Id, apiKey);
            var (success, errorMessage) = await tempProvider.TestConnectionAsync();

            _connectionSuccessful = success;

            if (success)
            {
                _connectionStatusLabel.Text = "Connected successfully";
                _connectionStatusLabel.ForeColor = Color.Green;
            }
            else
            {
                _connectionStatusLabel.Text = errorMessage ?? "Connection failed";
                _connectionStatusLabel.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            _connectionSuccessful = false;
            _connectionStatusLabel.Text = $"Error: {ex.Message}";
            _connectionStatusLabel.ForeColor = Color.Red;
        }
        finally
        {
            _testConnectionButton.Enabled = true;
        }
    }

    private void StartLogRefreshTimer()
    {
        _logRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _logRefreshTimer.Tick += (_, _) =>
        {
            // Only refresh if Logs tab is active
            if (_activeSection == "Logs")
            {
                RefreshLogs();
            }
            else if (_activeSection == "Journal")
            {
                RefreshJournal();
            }
        };
        _logRefreshTimer.Start();
    }

    private void RefreshLogs()
    {
        var logs = Logger.GetRecentLogs(200);
        if (_logTextBox.Text != logs)
        {
            _logTextBox.Text = logs;

            if (_autoScrollCheckBox.Checked)
            {
                _logTextBox.SelectionStart = _logTextBox.Text.Length;
                _logTextBox.ScrollToCaret();
            }
        }
    }

    private void ClearLogsButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var logPath = Logger.LogFilePath;
            if (File.Exists(logPath))
            {
                File.WriteAllText(logPath, string.Empty);
                Logger.Info("Logs cleared by user");
                RefreshLogs();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to clear logs: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var apiKey = _apiKeyTextBox.Text.Trim();
        var selectedProvider = (_providerCombo.SelectedItem as ProviderComboItem)?.Info;

        if (selectedProvider == null)
        {
            MessageBox.Show("Please select a provider.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Validate API key format
        if (!TranscriptionProviderFactory.ValidateApiKey(selectedProvider.Id, apiKey, out var errorMessage))
        {
            MessageBox.Show(errorMessage ?? "Invalid API key.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Parse shortcut modifiers
        uint shortcutModifiers = _shortcutCombo.SelectedIndex switch
        {
            0 => 0x0006, // MOD_CONTROL | MOD_SHIFT
            1 => 0x0001, // MOD_ALT
            2 => 0x0002, // MOD_CONTROL
            3 => 0x000C, // MOD_WIN | MOD_SHIFT
            _ => 0x0006
        };

        // Parse PTT key code (F1=0x70, F2=0x71, etc.)
        uint pttKeyCode = (uint)(0x70 + _pttCombo.SelectedIndex);
        var audioCaptureMode = (_audioCaptureModeCombo.SelectedItem as AudioCaptureModeComboItem)?.Mode ?? AudioCaptureMode.Instant;
        var processingModel = (_processingModelCombo.SelectedItem as ProcessingModelComboItem)?.Info.Id
            ?? ProcessingModelCatalog.DefaultModel;
        var customVocabulary = ParseVocabulary(_customVocabularyTextBox.Text);

        var providerApiKeys = new Dictionary<string, string>(
            _originalConfig.ProviderApiKeys ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            providerApiKeys[selectedProvider.Id] = apiKey;
        }

        var newConfig = new AppConfig
        {
            Provider = selectedProvider.Id,
            ApiKey = apiKey,
            ProviderApiKeys = providerApiKeys,
            ShortcutModifiers = shortcutModifiers,
            ShortcutKeyCode = 0x20, // VK_SPACE
            PushToTalkKeyCode = pttKeyCode,
            CustomVocabulary = customVocabulary,
            CustomModes = BuildCustomModesForSave(),
            DisabledBuiltInModeIds = _originalConfig.DisabledBuiltInModeIds?.ToList() ?? new List<string>(),
            AudioCaptureMode = audioCaptureMode,
            ProcessingModel = processingModel,
            AutoModeEnabled = _autoModeEnabledCheckBox.Checked,
            AutoModeFallbackToLastUsed = _autoModeFallbackCheckBox.Checked,
            AutoModeRules = BuildAutoModeRulesForSave(),
            PostActions = BuildPostActionsForSave(),
            ActivePostActionId = _activePostActionId,
            AutoPostActionEnabled = _autoPostActionEnabledCheckBox.Checked,
            AutoPostActionRules = BuildAutoPostActionRulesForSave(),
            ProjectTaggingEnabled = _projectTaggingEnabledCheckBox.Checked,
            LastUsedProjectId = _originalConfig.LastUsedProjectId
        };

        try
        {
            ProjectStore.SaveProjects(_projects);
            newConfig.Save();
            Logger.Info("Settings saved from Preferences window");
            SettingsSaved?.Invoke(newConfig);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _logRefreshTimer?.Stop();
        _logRefreshTimer?.Dispose();
        base.OnFormClosing(e);
    }

    private sealed record PreferenceSection(string Title, Panel Page, PreferenceNavButton Button);

    private sealed class PreferenceNavButton : Control
    {
        private bool _hovering;
        private bool _pressed;
        private bool _selected;

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        public PreferenceNavButton(string text)
        {
            Text = text;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
            Cursor = Cursors.Hand;
            BackColor = GlassTopColor;
            ForeColor = PrimaryTextColor;
            Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovering = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Focus();
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            var fillColor = Selected
                ? Color.FromArgb(238, 250, 254)
                : _pressed
                    ? Color.FromArgb(170, 225, 239)
                    : _hovering || Focused
                        ? Color.FromArgb(206, 238, 247)
                        : Color.FromArgb(181, 224, 238);
            var borderColor = Selected ? Color.White : Color.FromArgb(125, 194, 214);
            var textColor = Selected ? PrimaryTextColor : Color.FromArgb(68, 111, 130);

            using (var path = RoundedRect(bounds, 16))
            using (var fill = new SolidBrush(fillColor))
            using (var border = new Pen(borderColor, Selected ? 1.6f : 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            using var font = new Font("Segoe UI", 10f, Selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(
                g,
                Text,
                font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class HudDropdown : Control
    {
        private readonly ToolStripDropDown _dropDown = new();
        private readonly ListBox _listBox = new();
        private int _selectedIndex = -1;
        private bool _hovering;
        private bool _pressed;

        public List<object> Items { get; } = new();

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                var next = value;
                if (next < -1) next = -1;
                if (next >= Items.Count) next = Items.Count - 1;
                if (_selectedIndex == next) return;

                _selectedIndex = next;
                Text = SelectedItem?.ToString() ?? "";
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public object? SelectedItem =>
            _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;

        public event EventHandler? SelectedIndexChanged;

        public HudDropdown()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);

            BackColor = FieldColor;
            ForeColor = PrimaryTextColor;
            Cursor = Cursors.Hand;
            Height = 38;
            TabStop = true;

            _dropDown.Padding = Padding.Empty;
            _dropDown.Margin = Padding.Empty;
            _dropDown.AutoClose = true;
            _dropDown.BackColor = FieldColor;
            _dropDown.DropShadowEnabled = true;

            _listBox.BorderStyle = BorderStyle.None;
            _listBox.DrawMode = DrawMode.OwnerDrawFixed;
            _listBox.ItemHeight = 32;
            _listBox.BackColor = FieldColor;
            _listBox.ForeColor = PrimaryTextColor;
            _listBox.Font = new Font("Segoe UI", 10f);
            _listBox.IntegralHeight = false;
            _listBox.DrawItem += DrawListItem;
            _listBox.Click += (_, _) =>
            {
                if (_listBox.SelectedIndex >= 0)
                {
                    SelectedIndex = _listBox.SelectedIndex;
                    _dropDown.Close();
                    Focus();
                }
            };
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovering = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Focus();
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            if (ClientRectangle.Contains(e.Location))
            {
                ShowMenu();
            }
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space or Keys.Down)
            {
                ShowMenu();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Up && Items.Count > 0)
            {
                SelectedIndex = Math.Max(0, SelectedIndex - 1);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            var fillColor = _pressed
                ? Color.FromArgb(220, 246, 252)
                : _hovering || Focused
                    ? Color.FromArgb(248, 253, 255)
                    : FieldColor;

            using (var path = RoundedRect(bounds, Math.Min(14, Height / 2)))
            using (var fill = new SolidBrush(fillColor))
            using (var border = new Pen(Focused ? AccentColor : BorderColor, Focused ? 1.6f : 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            var text = SelectedItem?.ToString() ?? "";
            var textBounds = new Rectangle(12, 0, Math.Max(1, Width - 46), Height);
            TextRenderer.DrawText(
                g,
                text,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var cx = Width - 22;
            var cy = Height / 2 + 1;
            using var arrowBrush = new SolidBrush(Color.FromArgb(35, 58, 74));
            var arrow = new[]
            {
                new Point(cx - 5, cy - 3),
                new Point(cx + 5, cy - 3),
                new Point(cx, cy + 4)
            };
            g.FillPolygon(arrowBrush, arrow);
        }

        private void ShowMenu()
        {
            if (Items.Count == 0) return;

            _listBox.BeginUpdate();
            try
            {
                _listBox.Items.Clear();
                foreach (var item in Items)
                {
                    _listBox.Items.Add(item);
                }
                _listBox.SelectedIndex = Math.Clamp(SelectedIndex, 0, Items.Count - 1);
            }
            finally
            {
                _listBox.EndUpdate();
            }

            var height = Math.Min(Items.Count * _listBox.ItemHeight + 2, 292);
            _listBox.Size = new Size(Math.Max(Width, 220), height);

            _dropDown.Items.Clear();
            var host = new ToolStripControlHost(_listBox)
            {
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoSize = false,
                Size = _listBox.Size
            };
            _dropDown.Items.Add(host);
            _dropDown.Size = _listBox.Size;
            _dropDown.Show(this, new Point(0, Height + 4));
        }

        private void DrawListItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listBox.Items.Count) return;

            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using var fill = new SolidBrush(selected ? Color.FromArgb(58, 147, 190) : FieldColor);
            e.Graphics.FillRectangle(fill, e.Bounds);

            var color = selected ? Color.White : PrimaryTextColor;
            var bounds = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                _listBox.Items[e.Index]?.ToString() ?? "",
                _listBox.Font,
                bounds,
                color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private class ProviderComboItem
    {
        public ProviderInfo Info { get; }
        public ProviderComboItem(ProviderInfo info) => Info = info;
        public override string ToString() => Info.DisplayName;
    }

    private class AudioCaptureModeComboItem
    {
        public AudioCaptureMode Mode { get; }
        private readonly string _label;

        public AudioCaptureModeComboItem(AudioCaptureMode mode, string label)
        {
            Mode = mode;
            _label = label;
        }

        public override string ToString() => _label;
    }

    private class ProcessingModelComboItem
    {
        public ProcessingModelInfo Info { get; }
        public ProcessingModelComboItem(ProcessingModelInfo info) => Info = info;
        public override string ToString() => Info.DisplayName;
    }

    private class BuiltInModeListItem
    {
        private readonly AIMode _mode;

        public BuiltInModeListItem(AIMode mode) => _mode = mode;

        public override string ToString()
        {
            var detail = _mode.Id switch
            {
                "voice-to-text" => "raw transcription",
                "super" => "context-aware assistant",
                _ => "AI processing"
            };

            return $"{_mode.Name} - {detail}";
        }
    }

    private void RefreshJournal()
    {
        var entries = RecordingJournal.GetRecentEntries(100);
        var selectedId = (_journalGrid.CurrentRow?.Tag as RecordingJournalEntry)?.Id;

        _journalGrid.Rows.Clear();

        foreach (var entry in entries)
        {
            var rowIndex = _journalGrid.Rows.Add(
                entry.StartedAt.ToString("HH:mm:ss"),
                entry.Status,
                entry.Mode,
                entry.ProviderName,
                FormatMs(entry.TotalMs),
                FormatStepMs(entry, "record_audio"),
                FormatStepMs(entry, "transcribe_audio"),
                FormatStepMs(entry, "ai_processing"),
                FormatStepMs(entry, "post_action", "paste_result"));

            var row = _journalGrid.Rows[rowIndex];
            row.Tag = entry;

            if (entry.Status == "failed")
            {
                row.DefaultCellStyle.ForeColor = Color.DarkRed;
            }
            else if (entry.Status == "cancelled")
            {
                row.DefaultCellStyle.ForeColor = Color.DarkOrange;
            }

            if (entry.Id == selectedId)
            {
                row.Selected = true;
                _journalGrid.CurrentCell = row.Cells[0];
            }
        }

        UpdateJournalDetails();
    }

    private void UpdateJournalDetails()
    {
        if (_journalGrid.CurrentRow?.Tag is not RecordingJournalEntry entry)
        {
            _journalDetailsTextBox.Text = "No recording selected.";
            return;
        }

        var lines = new List<string>
        {
            $"Recording {entry.Id} - {entry.Status}",
            $"Started: {entry.StartedAt:yyyy-MM-dd HH:mm:ss.fff}",
            $"Provider: {entry.ProviderName} ({entry.ProviderId})",
            $"Mode: {entry.Mode}",
            $"Total: {FormatMs(entry.TotalMs)}",
            $"Audio: {(entry.AudioBytes.HasValue ? FormatBytes(entry.AudioBytes.Value) : "-")}",
            $"Text: raw={entry.RawTextChars?.ToString() ?? "-"} final={entry.FinalTextChars?.ToString() ?? "-"}",
            $"Action paste: {(entry.Pasted.HasValue ? (entry.Pasted.Value ? "ok" : "not pasted") : "-")}"
        };

        if (!string.IsNullOrWhiteSpace(entry.Error))
        {
            lines.Add($"Error: {entry.Error}");
        }

        lines.Add("");
        lines.Add("Steps:");

        foreach (var step in entry.Steps.OrderBy(step => step.Order))
        {
            var detail = string.IsNullOrWhiteSpace(step.Detail) ? "" : $" - {step.Detail}";
            var error = string.IsNullOrWhiteSpace(step.Error) ? "" : $" - ERROR: {step.Error}";
            lines.Add($"{step.Order,2}. {step.Name,-24} {FormatMs(step.DurationMs),8}  {step.Status}{detail}{error}");
        }

        _journalDetailsTextBox.Text = string.Join(Environment.NewLine, lines);
    }

    private void ClearJournalButton_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Clear the recording journal?",
            "Clear Journal",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        try
        {
            RecordingJournal.Clear();
            RefreshJournal();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to clear journal: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string FormatStepMs(RecordingJournalEntry entry, params string[] stepNames)
    {
        var step = entry.Steps.FirstOrDefault(step => stepNames.Any(stepName =>
            string.Equals(step.Name, stepName, StringComparison.OrdinalIgnoreCase)));
        return step == null ? "-" : FormatMs(step.DurationMs);
    }

    private static string FormatMs(int ms) =>
        ms >= 1000 ? $"{ms / 1000.0:0.00}s" : $"{ms}ms";

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.00} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }

    private class CustomModeListItem
    {
        public int Index { get; }
        public CustomModeConfig Mode { get; }

        public CustomModeListItem(int index, CustomModeConfig mode)
        {
            Index = index;
            Mode = mode;
        }

        public override string ToString()
        {
            var suffix = Mode.Enabled ? "" : " (disabled)";
            return $"{Mode.Name}{suffix}";
        }
    }

    private class PostActionListItem
    {
        public int Index { get; }
        public PostActionConfig Action { get; }
        private readonly string _activeId;

        public PostActionListItem(int index, PostActionConfig action, string activeId)
        {
            Index = index;
            Action = action;
            _activeId = activeId;
        }

        public override string ToString()
        {
            var prefix = string.Equals(Action.Id, _activeId, StringComparison.OrdinalIgnoreCase) ? "[active] " : "";
            var kind = Action.IsBuiltIn ? "built-in" : "command";
            return $"{prefix}{Action.Label} - {PostActionConfig.Describe(Action)} ({kind})";
        }
    }

    private class AutoModeRuleListItem
    {
        public int Index { get; }
        public AutoModeRuleConfig Rule { get; }

        public AutoModeRuleListItem(int index, AutoModeRuleConfig rule)
        {
            Index = index;
            Rule = rule;
        }
    }

    private class AutoPostActionRuleListItem
    {
        public int Index { get; }
        public AutoPostActionRuleConfig Rule { get; }

        public AutoPostActionRuleListItem(int index, AutoPostActionRuleConfig rule)
        {
            Index = index;
            Rule = rule;
        }
    }

    private class ProjectListItem
    {
        public ProjectConfig Project { get; }

        public ProjectListItem(ProjectConfig project)
        {
            Project = project;
        }

        public override string ToString()
        {
            var suffix = Project.Archived ? " (archived)" : "";
            return $"{Project.Name}{suffix}";
        }
    }
}
