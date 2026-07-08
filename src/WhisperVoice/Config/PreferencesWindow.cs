using System.Drawing.Drawing2D;
using WhisperVoice.Api;
using WhisperVoice.History;
using WhisperVoice.Logging;
using WhisperVoice.Processing;

namespace WhisperVoice.Config;

public class PreferencesWindow : Form
{
    private static readonly Color SurfaceColor = Color.FromArgb(255, 255, 255);
    private static readonly Color SurfaceAltColor = Color.FromArgb(247, 247, 245);
    private static readonly Color HoverColor = Color.FromArgb(247, 247, 245);
    private static readonly Color FieldColor = Color.FromArgb(255, 255, 255);
    private static readonly Color PrimaryTextColor = Color.FromArgb(47, 52, 55);
    private static readonly Color MutedTextColor = Color.FromArgb(138, 138, 133);
    private static readonly Color AccentColor = Color.FromArgb(35, 131, 226);
    private static readonly Color AccentHoverColor = Color.FromArgb(24, 102, 181);
    private static readonly Color PrimaryButtonColor = Color.FromArgb(47, 52, 55);
    private static readonly Color PrimaryButtonHoverColor = Color.FromArgb(17, 19, 21);
    private static readonly Color BorderColor = Color.FromArgb(236, 235, 234);
    private static readonly Color BorderSoftColor = Color.FromArgb(243, 242, 241);
    private const int SidebarWidth = 214;
    private const int ContentMaxWidth = 760;

    private FlowLayoutPanel _navPanel = null!;
    private Panel _contentPanel = null!;
    private readonly List<PreferenceSection> _sections = new();
    private string _activeSection = "General";

    // General tab
    private ComboBox _providerCombo = null!;
    private TextBox _apiKeyTextBox = null!;
    private LinkLabel _apiKeyLink = null!;
    private Button _testConnectionButton = null!;
    private Label _connectionStatusLabel = null!;
    private ComboBox _audioCaptureModeCombo = null!;
    private Label _audioCaptureModeDescriptionLabel = null!;
    private ComboBox _processingModelCombo = null!;
    private TextBox _customVocabularyTextBox = null!;

    // Shortcuts tab
    private ComboBox _shortcutCombo = null!;
    private ComboBox _pttCombo = null!;

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
    private ComboBox _projectDefaultModeCombo = null!;
    private TextBox _projectContextNotesTextBox = null!;
    private ListBox _projectDocumentsList = null!;
    private Button _renameProjectButton = null!;
    private Button _archiveProjectButton = null!;
    private Button _importProjectFileButton = null!;
    private Button _removeProjectDocumentButton = null!;
    private readonly List<ProjectConfig> _projects = new();
    private bool _loadingProjectDetails;

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
    private FooterBarPanel _footerPanel = null!;

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
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1040, 720);
        MinimumSize = new Size(920, 640);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = SurfaceColor;
        DoubleBuffered = true;

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = SurfaceColor,
            Padding = new Padding(20, 18, 20, 0)
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));

        _navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 4, 16, 0),
            Margin = new Padding(0, 0, 18, 0)
        };

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(0, 0, 0, 16),
            Margin = Padding.Empty
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

        _footerPanel = new FooterBarPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 20, 12),
            BackColor = SurfaceColor,
            Margin = Padding.Empty
        };

        _saveButton = new Button
        {
            Text = "Save",
            Size = new Size(124, 46),
            MinimumSize = new Size(124, 46),
            Margin = Padding.Empty,
            FlatStyle = FlatStyle.Flat
        };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(124, 46),
            MinimumSize = new Size(124, 46),
            Margin = Padding.Empty,
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.Click += (_, _) => Close();

        _footerPanel.Controls.Add(_cancelButton);
        _footerPanel.Controls.Add(_saveButton);
        _footerPanel.Resize += (_, _) => LayoutFooterButtons();

        shell.Controls.Add(_navPanel, 0, 0);
        shell.Controls.Add(_contentPanel, 1, 0);
        shell.Controls.Add(_footerPanel, 0, 1);
        shell.SetColumnSpan(_footerPanel, 2);
        Controls.Add(shell);

        ApplyPreferencesTheme();
        LayoutFooterButtons();
        SelectPreferenceSection("General");
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(SurfaceColor);
    }

    private void AddPreferenceSection(string title, Panel page)
    {
        page.Text = title;
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        page.Margin = Padding.Empty;

        var navButton = new PreferenceNavButton(title)
        {
            Width = SidebarWidth - 34,
            Height = 36,
            Margin = new Padding(0, 0, 0, 4)
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

        if (string.Equals(title, "Projects", StringComparison.OrdinalIgnoreCase))
        {
            LoadSelectedProjectDetails();
        }
    }

    private void ApplyPreferencesTheme()
    {
        _contentPanel.BackColor = SurfaceColor;
        _navPanel.BackColor = SurfaceColor;

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
        LayoutFooterButtons();
    }

    private void LayoutFooterButtons()
    {
        if (_footerPanel == null || _saveButton == null || _cancelButton == null) return;

        const int gap = 10;
        var buttonHeight = Math.Max(_saveButton.Height, _cancelButton.Height);
        var y = Math.Max(
            _footerPanel.Padding.Top,
            (_footerPanel.ClientSize.Height - buttonHeight) / 2);
        var right = _footerPanel.ClientSize.Width - _footerPanel.Padding.Right;

        _saveButton.Location = new Point(right - _saveButton.Width, y);
        _cancelButton.Location = new Point(_saveButton.Left - gap - _cancelButton.Width, y);
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
            case ComboBox dropdown:
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
                checkBox.Font = new Font("Segoe UI", 9.6f);
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
        var isMonospace = string.Equals(textBox.Font.FontFamily.Name, "Consolas", StringComparison.OrdinalIgnoreCase);
        if (!isMonospace)
        {
            textBox.Font = new Font("Segoe UI", textBox.Multiline ? 9.8f : 9.6f);
        }
        if (!textBox.Multiline && textBox.Height < 34)
        {
            textBox.AutoSize = false;
            textBox.Height = 34;
        }
    }

    private static void StyleDropdown(ComboBox dropdown)
    {
        dropdown.BackColor = FieldColor;
        dropdown.ForeColor = PrimaryTextColor;
        dropdown.Font = new Font("Segoe UI", 9.6f);
        dropdown.FlatStyle = FlatStyle.Standard;
        dropdown.DropDownStyle = ComboBoxStyle.DropDownList;
        dropdown.IntegralHeight = false;
    }

    private static void StyleListBox(ListBox listBox)
    {
        listBox.BackColor = FieldColor;
        listBox.ForeColor = PrimaryTextColor;
        listBox.BorderStyle = BorderStyle.FixedSingle;
        listBox.Font = new Font("Segoe UI", 9.6f);
        listBox.ItemHeight = Math.Max(listBox.ItemHeight, 28);
        listBox.DrawMode = DrawMode.OwnerDrawFixed;
        listBox.DrawItem -= DrawListBoxItem;
        listBox.DrawItem += DrawListBoxItem;
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = SurfaceColor;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = BorderSoftColor;
        grid.EnableHeadersVisualStyles = false;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAltColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryTextColor;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
        grid.ColumnHeadersHeight = 36;
        grid.DefaultCellStyle.BackColor = FieldColor;
        grid.DefaultCellStyle.ForeColor = PrimaryTextColor;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 242, 253);
        grid.DefaultCellStyle.SelectionForeColor = PrimaryTextColor;
        grid.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 251);
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 32);
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.BackColor = primary ? PrimaryButtonColor : SurfaceColor;
        button.ForeColor = primary ? Color.White : PrimaryTextColor;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? PrimaryButtonColor : BorderColor;
        button.FlatAppearance.MouseOverBackColor = primary ? PrimaryButtonHoverColor : HoverColor;
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(0, 0, 0) : BorderSoftColor;
        button.Font = new Font("Segoe UI", 9.4f, primary ? FontStyle.Bold : FontStyle.Regular);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.AutoSize = false;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Padding = new Padding(10, 0, 10, 0);
        FitButtonToText(button);
        button.Resize -= RoundButtonOnResize;
        button.Region?.Dispose();
        button.Region = null;
    }

    private static void FitButtonToText(Button button)
    {
        var textSize = TextRenderer.MeasureText(
            button.Text,
            button.Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        var minWidth = Math.Max(112, textSize.Width + 52);
        var minHeight = Math.Max(42, textSize.Height + 20);
        button.MinimumSize = new Size(minWidth, minHeight);
        button.Size = new Size(Math.Max(button.Width, minWidth), Math.Max(button.Height, minHeight));
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
        var back = selected ? Color.FromArgb(232, 242, 253) : listBox.BackColor;
        var fore = PrimaryTextColor;

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
        button.Region = new Region(RoundedRect(new Rectangle(0, 0, button.Width, button.Height), 6));
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

    private static Panel CreatePreferencesPage(bool autoScroll = false)
    {
        return new Panel
        {
            AutoScroll = autoScroll,
            Padding = new Padding(0, 4, 8, 0),
            BackColor = SurfaceColor
        };
    }

    private static TableLayoutPanel CreateSettingsStack(Control host)
    {
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(0, 0, 0, 28),
            Margin = Padding.Empty,
            Width = ContentMaxWidth,
            MinimumSize = new Size(320, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ConstrainStackWidth(host, table);
        host.SizeChanged += (_, _) => ConstrainStackWidth(host, table);
        return table;
    }

    private static void ConstrainStackWidth(Control host, Control stack)
    {
        var available = Math.Max(320, host.ClientSize.Width - host.Padding.Horizontal - 16);
        stack.Width = Math.Min(ContentMaxWidth, available);
    }

    private static void AddStackSection(TableLayoutPanel table, string text)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 0 ? 42 : 58));

        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.4f, FontStyle.Bold),
            ForeColor = PrimaryTextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, row == 0 ? 0 : 18, 0, 0),
            Margin = Padding.Empty
        };

        table.Controls.Add(label, 0, row);
    }

    private static void AddStackField(
        TableLayoutPanel table,
        string labelText,
        Control control,
        string? hint = null,
        int controlHeight = 34,
        int hintHeight = 28)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var field = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 1,
            RowCount = string.IsNullOrWhiteSpace(hint) ? 2 : 3,
            Margin = new Padding(0, 0, 0, 18),
            Padding = Padding.Empty,
            Width = table.Width
        };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.SizeChanged += (_, _) => field.Width = table.Width;
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 6)
        };

        control.Dock = DockStyle.Top;
        control.Margin = Padding.Empty;
        if (control is TextBox { Multiline: true } || control is ListBox || control is DataGridView)
        {
            control.Height = controlHeight;
        }
        else
        {
            control.MinimumSize = new Size(0, controlHeight);
        }

        field.Controls.Add(label, 0, 0);
        field.Controls.Add(control, 0, 1);

        if (!string.IsNullOrWhiteSpace(hint))
        {
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.Controls.Add(CreateHintLabel(hint, hintHeight), 0, 2);
        }

        table.Controls.Add(field, 0, row);
        table.SetColumn(field, 0);
    }

    private static Label CreateHintLabel(string text, int height = 34)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(ContentMaxWidth, 0),
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 7, 0, 0),
            Margin = new Padding(0, 0, 0, 0)
        };
    }

    private static Label CreateInlineStatusLabel()
    {
        return new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 0, 0, 0)
        };
    }

    private static FlowLayoutPanel CreateButtonRow()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };
    }

    private static void AddStackElement(TableLayoutPanel table, Control control, int bottomMargin = 18)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Dock = DockStyle.Top;
        control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        control.Width = table.Width;
        control.Margin = new Padding(0, 0, 0, bottomMargin);
        table.Controls.Add(control, 0, row);
        table.SizeChanged += (_, _) => control.Width = table.Width;
    }

    private static ComboBox CreateDropdown()
    {
        return new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Standard,
            IntegralHeight = false,
            Margin = Padding.Empty
        };
    }

    private Panel CreateGeneralTab()
    {
        var tab = CreatePreferencesPage(autoScroll: true);
        var root = CreateSettingsStack(tab);

        AddStackSection(root, "Transcription");

        _providerCombo = CreateDropdown();

        foreach (var provider in TranscriptionProviderFactory.GetAvailableProviders())
        {
            _providerCombo.Items.Add(new ProviderComboItem(provider));
        }
        _providerCombo.SelectedIndexChanged += ProviderCombo_Changed;
        AddStackField(root, "Provider", _providerCombo);

        var apiKeyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        apiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        apiKeyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        apiKeyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        apiKeyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _apiKeyTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            PlaceholderText = "sk-..."
        };
        _apiKeyTextBox.TextChanged += (_, _) => ResetConnectionStatus();

        _apiKeyLink = new LinkLabel
        {
            Text = "Get API key",
            Dock = DockStyle.Top,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0, 4, 0, 0)
        };
        _apiKeyLink.Click += ApiKeyLink_Click;

        _testConnectionButton = new Button
        {
            Text = "Test Connection",
            Size = new Size(132, 32),
            FlatStyle = FlatStyle.Flat
        };
        _testConnectionButton.Click += TestConnectionButton_Click;

        _connectionStatusLabel = CreateInlineStatusLabel();

        var testRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 0),
            Margin = Padding.Empty
        };
        testRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        testRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        testRow.Controls.Add(_testConnectionButton, 0, 0);
        testRow.Controls.Add(_connectionStatusLabel, 1, 0);

        apiKeyPanel.Controls.Add(_apiKeyTextBox, 0, 0);
        apiKeyPanel.Controls.Add(_apiKeyLink, 0, 1);
        apiKeyPanel.Controls.Add(testRow, 0, 2);
        AddStackField(root, "API key", apiKeyPanel, controlHeight: 106);

        var audioPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        audioPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        audioPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        audioPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _audioCaptureModeCombo = CreateDropdown();
        _audioCaptureModeCombo.Items.AddRange(new object[]
        {
            new AudioCaptureModeComboItem(AudioCaptureMode.Instant, "Instant"),
            new AudioCaptureModeComboItem(AudioCaptureMode.Balanced, "Balanced"),
            new AudioCaptureModeComboItem(AudioCaptureMode.Privacy, "Privacy")
        });
        _audioCaptureModeCombo.SelectedIndexChanged += (_, _) => UpdateAudioCaptureModeDescription();

        _audioCaptureModeDescriptionLabel = CreateHintLabel("", 44);
        audioPanel.Controls.Add(_audioCaptureModeCombo, 0, 0);
        audioPanel.Controls.Add(_audioCaptureModeDescriptionLabel, 0, 1);
        AddStackField(root, "Latency", audioPanel, controlHeight: 78);

        AddStackSection(root, "AI processing");

        _processingModelCombo = CreateDropdown();
        foreach (var model in ProcessingModelCatalog.GetAvailableModels())
        {
            _processingModelCombo.Items.Add(new ProcessingModelComboItem(model));
        }
        AddStackField(
            root,
            "Model",
            _processingModelCombo,
            "Used by Clean, Formel, Casual, Markdown, Super and custom modes. Brut keeps the raw transcription.",
            hintHeight: 42);

        _customVocabularyTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "PostHog, Kubernetes, Chatwoot, project names..."
        };
        AddStackField(
            root,
            "Vocabulary",
            _customVocabularyTextBox,
            "Comma-separated terms sent to transcription, then preserved during AI processing.",
            controlHeight: 84,
            hintHeight: 36);

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateShortcutsTab()
    {
        var tab = CreatePreferencesPage(autoScroll: true);
        var root = CreateSettingsStack(tab);

        AddStackSection(root, "Recording shortcuts");

        _shortcutCombo = CreateDropdown();
        _shortcutCombo.Items.AddRange(new object[]
        {
            "Ctrl+Shift+Space (recommended)",
            "Alt+Space",
            "Ctrl+Space",
            "Win+Shift+Space"
        });
        AddStackField(root, "Toggle", _shortcutCombo, "Starts or stops a recording.");

        _pttCombo = CreateDropdown();
        _pttCombo.Items.AddRange(new object[]
        {
            "F1", "F2", "F3 (recommended)", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
        });
        AddStackField(root, "Push-to-talk", _pttCombo, "Hold this key to record.");

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateModesTab()
    {
        var tab = CreatePreferencesPage();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(0)
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

        var buttonPanel = CreateButtonRow();

        var addModeButton = new Button
        {
            Text = "Add",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        addModeButton.Click += AddModeButton_Click;

        _editModeButton = new Button
        {
            Text = "Edit",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editModeButton.Click += (_, _) => EditSelectedMode();

        _deleteModeButton = new Button
        {
            Text = "Delete",
            Size = new Size(88, 32),
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
        var tab = CreatePreferencesPage();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0)
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
        _autoModeRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "App", FillWeight = 90 });
        _autoModeRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", FillWeight = 130 });
        _autoModeRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode", FillWeight = 90 });
        _autoModeRulesGrid.SelectionChanged += (_, _) => UpdateAutoModeRuleButtons();
        _autoModeRulesGrid.DoubleClick += (_, _) => EditSelectedAutoModeRule();

        var buttonPanel = CreateButtonRow();

        var addRuleButton = new Button
        {
            Text = "Add",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        addRuleButton.Click += AddAutoModeRuleButton_Click;

        _editAutoModeRuleButton = new Button
        {
            Text = "Edit",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editAutoModeRuleButton.Click += (_, _) => EditSelectedAutoModeRule();

        _deleteAutoModeRuleButton = new Button
        {
            Text = "Delete",
            Size = new Size(88, 32),
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
        var tab = CreatePreferencesPage(autoScroll: true);
        var root = CreateSettingsStack(tab);

        AddStackSection(root, "Post-transcription actions");

        _postActionsList = new ListBox
        {
            Dock = DockStyle.Top,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Height = 132,
            Margin = Padding.Empty
        };
        _postActionsList.SelectedIndexChanged += (_, _) => UpdatePostActionButtons();
        _postActionsList.DoubleClick += (_, _) => EditSelectedPostAction();
        AddStackField(root, "Active action", _postActionsList, controlHeight: 132);

        var buttonPanel = CreateButtonRow();

        _setActivePostActionButton = new Button
        {
            Text = "Set Active",
            Size = new Size(96, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _setActivePostActionButton.Click += (_, _) => SetSelectedPostActionActive();

        var addActionButton = new Button
        {
            Text = "Add",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        addActionButton.Click += AddPostActionButton_Click;

        _editPostActionButton = new Button
        {
            Text = "Edit",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editPostActionButton.Click += (_, _) => EditSelectedPostAction();

        _deletePostActionButton = new Button
        {
            Text = "Delete",
            Size = new Size(88, 32),
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
        AddStackElement(root, buttonPanel, bottomMargin: 22);

        _autoPostActionEnabledCheckBox = new CheckBox
        {
            Text = "Automatically select action from the active app or window title",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        _autoPostActionEnabledCheckBox.CheckedChanged += (_, _) =>
        {
            _autoPostActionRulesGrid.Enabled = _autoPostActionEnabledCheckBox.Checked;
            UpdateAutoPostActionRuleButtons();
        };
        AddStackElement(root, _autoPostActionEnabledCheckBox, bottomMargin: 10);

        _autoPostActionRulesGrid = new DataGridView
        {
            Dock = DockStyle.Top,
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
            Height = 150,
            Margin = Padding.Empty
        };
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 36 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Rule", FillWeight = 140 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "App", FillWeight = 90 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", FillWeight = 130 });
        _autoPostActionRulesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action", FillWeight = 100 });
        _autoPostActionRulesGrid.SelectionChanged += (_, _) => UpdateAutoPostActionRuleButtons();
        _autoPostActionRulesGrid.DoubleClick += (_, _) => EditSelectedAutoPostActionRule();
        AddStackField(root, "Action rules", _autoPostActionRulesGrid, controlHeight: 150);

        var autoButtonPanel = CreateButtonRow();

        var addAutoRuleButton = new Button
        {
            Text = "Add",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        addAutoRuleButton.Click += AddAutoPostActionRuleButton_Click;

        _editAutoPostActionRuleButton = new Button
        {
            Text = "Edit",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _editAutoPostActionRuleButton.Click += (_, _) => EditSelectedAutoPostActionRule();

        _deleteAutoPostActionRuleButton = new Button
        {
            Text = "Delete",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _deleteAutoPostActionRuleButton.Click += DeleteAutoPostActionRuleButton_Click;

        autoButtonPanel.Controls.AddRange(new Control[] { addAutoRuleButton, _editAutoPostActionRuleButton, _deleteAutoPostActionRuleButton });
        AddStackElement(root, autoButtonPanel, bottomMargin: 14);

        AddStackElement(
            root,
            CreateHintLabel("Command actions receive transcription and context variables. Auto-action rules override the active action only when enabled and matched."),
            bottomMargin: 0);

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateLogsTab()
    {
        var tab = CreatePreferencesPage();
        tab.Padding = new Padding(0, 4, 18, 12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = SurfaceColor,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 14),
            Padding = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "Logs",
            AutoSize = true,
            Font = new Font("Segoe UI", 10.4f, FontStyle.Bold),
            ForeColor = PrimaryTextColor,
            Margin = Padding.Empty
        };

        _autoScrollCheckBox = new CheckBox
        {
            Text = "Auto-scroll",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(12, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };

        header.Controls.Add(titleLabel, 0, 0);
        header.Controls.Add(_autoScrollCheckBox, 1, 0);

        var currentLogLabel = new Label
        {
            Text = "Current log",
            AutoSize = true,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 0, 0, 8)
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 8.5F),
            WordWrap = false,
            Margin = new Padding(0, 0, 0, 14)
        };

        var buttonPanel = CreateButtonRow();

        var clearLogsButton = new Button
        {
            Text = "Clear",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        clearLogsButton.Click += ClearLogsButton_Click;

        var openLogFolderButton = new Button
        {
            Text = "Open Folder",
            Size = new Size(112, 32),
            FlatStyle = FlatStyle.Flat
        };
        openLogFolderButton.Click += (_, _) => Logger.OpenLogFolder();

        buttonPanel.Controls.AddRange(new Control[] { clearLogsButton, openLogFolderButton });

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(currentLogLabel, 0, 1);
        root.Controls.Add(_logTextBox, 0, 2);
        root.Controls.Add(buttonPanel, 0, 3);

        tab.Controls.Add(root);

        return tab;
    }

    private Panel CreateProjectsTab()
    {
        var tab = CreatePreferencesPage(autoScroll: true);
        var root = CreateSettingsStack(tab);

        _projectTaggingEnabledCheckBox = new CheckBox
        {
            Text = "Predict and save projects for recordings",
            AutoSize = true,
            Margin = Padding.Empty
        };
        AddStackElement(root, _projectTaggingEnabledCheckBox, bottomMargin: 18);
        AddStackSection(root, "Projects");

        _projectsList = new ListBox
        {
            Dock = DockStyle.Top,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Height = 170,
            Margin = Padding.Empty
        };
        _projectsList.SelectedIndexChanged += (_, _) =>
        {
            UpdateProjectButtons();
            LoadSelectedProjectDetails();
        };
        _projectsList.DoubleClick += (_, _) => RenameSelectedProject();
        AddStackField(root, "Project list", _projectsList, controlHeight: 170);

        var buttonPanel = CreateButtonRow();

        var addProjectButton = new Button
        {
            Text = "Add",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        addProjectButton.Click += AddProjectButton_Click;

        _renameProjectButton = new Button
        {
            Text = "Rename",
            Size = new Size(96, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _renameProjectButton.Click += (_, _) => RenameSelectedProject();

        _archiveProjectButton = new Button
        {
            Text = "Archive",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _archiveProjectButton.Click += ArchiveProjectButton_Click;

        buttonPanel.Controls.AddRange(new Control[] { addProjectButton, _renameProjectButton, _archiveProjectButton });
        AddStackElement(root, buttonPanel, bottomMargin: 16);

        AddStackSection(root, "Selected project");

        _projectDefaultModeCombo = CreateDropdown();
        _projectDefaultModeCombo.SelectedIndexChanged += (_, _) => UpdateSelectedProjectFromDetails();
        AddStackField(root, "Default mode", _projectDefaultModeCombo);

        _projectContextNotesTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 120,
            PlaceholderText = "Add anything Whisper Voice should know for this project: constraints, vocabulary, people, expected tone, decision history..."
        };
        _projectContextNotesTextBox.TextChanged += (_, _) => UpdateSelectedProjectFromDetails();
        AddStackField(root, "Context notes", _projectContextNotesTextBox, controlHeight: 120);

        _projectDocumentsList = new ListBox
        {
            Dock = DockStyle.Top,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Height = 130,
            Margin = Padding.Empty
        };
        _projectDocumentsList.SelectedIndexChanged += (_, _) => UpdateProjectButtons();
        AddStackField(root, "Imported context files", _projectDocumentsList, controlHeight: 130);

        var documentsButtonPanel = CreateButtonRow();

        _importProjectFileButton = new Button
        {
            Text = "Import",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _importProjectFileButton.Click += ImportProjectFileButton_Click;

        _removeProjectDocumentButton = new Button
        {
            Text = "Remove",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _removeProjectDocumentButton.Click += RemoveProjectDocumentButton_Click;

        documentsButtonPanel.Controls.AddRange(new Control[] { _importProjectFileButton, _removeProjectDocumentButton });
        AddStackElement(root, documentsButtonPanel, bottomMargin: 14);
        AddStackElement(
            root,
            CreateHintLabel("Project context is used only by AI modes and is added explicitly to the prompt for the selected or predicted project."),
            bottomMargin: 0);

        tab.Controls.Add(root);
        return tab;
    }

    private Panel CreateJournalTab()
    {
        var tab = CreatePreferencesPage();
        tab.Padding = new Padding(0, 4, 18, 12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = SurfaceColor,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "Journal",
            AutoSize = true,
            Font = new Font("Segoe UI", 10.4f, FontStyle.Bold),
            ForeColor = PrimaryTextColor,
            Margin = new Padding(0, 0, 0, 10)
        };

        var recentLabel = new Label
        {
            Text = "Recent recordings",
            AutoSize = true,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 0, 0, 8)
        };

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
            Margin = new Padding(0, 0, 0, 16)
        };
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time", FillWeight = 90, MinimumWidth = 86 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 92, MinimumWidth = 92 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode", FillWeight = 115, MinimumWidth = 105 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Provider", HeaderText = "Provider", FillWeight = 160, MinimumWidth = 150 });
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total", FillWeight = 78, MinimumWidth = 82 });
        _journalGrid.SelectionChanged += (_, _) => UpdateJournalDetails();

        var selectedLabel = new Label
        {
            Text = "Selected recording",
            AutoSize = true,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 0, 0, 8)
        };

        _journalDetailsTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 8.5F),
            Margin = new Padding(0, 0, 0, 14)
        };

        var buttonPanel = CreateButtonRow();

        var refreshButton = new Button
        {
            Text = "Refresh",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        refreshButton.Click += (_, _) => RefreshJournal();

        var openFolderButton = new Button
        {
            Text = "Open Folder",
            Size = new Size(112, 32),
            FlatStyle = FlatStyle.Flat
        };
        openFolderButton.Click += (_, _) => RecordingJournal.OpenFolder();

        var clearButton = new Button
        {
            Text = "Clear",
            Size = new Size(88, 32),
            FlatStyle = FlatStyle.Flat
        };
        clearButton.Click += ClearJournalButton_Click;

        buttonPanel.Controls.AddRange(new Control[] { refreshButton, openFolderButton, clearButton });

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(recentLabel, 0, 1);
        root.Controls.Add(_journalGrid, 0, 2);
        root.Controls.Add(selectedLabel, 0, 3);
        root.Controls.Add(_journalDetailsTextBox, 0, 4);
        root.Controls.Add(buttonPanel, 0, 5);

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
        SelectProjectByName(dialog.ProjectName);
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

    private void ImportProjectFileButton_Click(object? sender, EventArgs e)
    {
        var project = GetSelectedProject();
        if (project == null) return;

        using var dialog = new OpenFileDialog
        {
            Title = "Import project context file",
            Filter = "Text files (*.txt;*.md;*.csv;*.json;*.log)|*.txt;*.md;*.csv;*.json;*.log|All files (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var imported = 0;
        foreach (var fileName in dialog.FileNames)
        {
            try
            {
                project.Documents.Add(ProjectStore.ImportDocument(fileName));
                imported++;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import '{Path.GetFileName(fileName)}': {ex.Message}", "Import Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        if (imported > 0)
        {
            RefreshProjectDocumentsList(project);
        }
    }

    private void RemoveProjectDocumentButton_Click(object? sender, EventArgs e)
    {
        var project = GetSelectedProject();
        if (project == null || _projectDocumentsList.SelectedItem is not ProjectDocumentListItem item) return;

        var result = MessageBox.Show(
            $"Remove '{item.Document.FileName}' from this project context?",
            "Remove Context File",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        project.Documents.Remove(item.Document);
        RefreshProjectDocumentsList(project);
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
        LoadSelectedProjectDetails();
    }

    private void UpdateProjectButtons()
    {
        var item = _projectsList?.SelectedItem as ProjectListItem;
        var hasSelection = item != null;
        _renameProjectButton.Enabled = hasSelection;
        _archiveProjectButton.Enabled = hasSelection && item?.Project.Archived == false;
        if (_importProjectFileButton != null)
        {
            _importProjectFileButton.Enabled = hasSelection && item?.Project.Archived == false;
        }

        if (_removeProjectDocumentButton != null)
        {
            _removeProjectDocumentButton.Enabled =
                hasSelection &&
                item?.Project.Archived == false &&
                _projectDocumentsList?.SelectedItem is ProjectDocumentListItem;
        }
    }

    private ProjectConfig? GetSelectedProject() =>
        (_projectsList?.SelectedItem as ProjectListItem)?.Project;

    private void LoadSelectedProjectDetails()
    {
        if (_projectDefaultModeCombo == null || _projectContextNotesTextBox == null || _projectDocumentsList == null)
        {
            return;
        }

        var project = GetSelectedProject();
        _loadingProjectDetails = true;
        try
        {
            RefreshProjectModeCombo(project?.DefaultModeId);
            _projectContextNotesTextBox.Text = project?.ContextNotes ?? "";
            _projectContextNotesTextBox.Enabled = project != null && !project.Archived;
            _projectDefaultModeCombo.Enabled = project != null && !project.Archived;
            RefreshProjectDocumentsList(project);
        }
        finally
        {
            _loadingProjectDetails = false;
        }

        UpdateProjectButtons();
    }

    private void RefreshProjectModeCombo(string? selectedModeId)
    {
        _projectDefaultModeCombo.Items.Clear();
        _projectDefaultModeCombo.Items.Add(new ProjectModeComboItem(null));

        foreach (var mode in GetAvailableModesForAutoRules())
        {
            _projectDefaultModeCombo.Items.Add(new ProjectModeComboItem(mode));
        }

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedModeId))
        {
            for (var i = 0; i < _projectDefaultModeCombo.Items.Count; i++)
            {
                if (_projectDefaultModeCombo.Items[i] is ProjectModeComboItem item &&
                    string.Equals(item.ModeId, selectedModeId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        _projectDefaultModeCombo.SelectedIndex = selectedIndex;
    }

    private void UpdateSelectedProjectFromDetails()
    {
        if (_loadingProjectDetails) return;

        var project = GetSelectedProject();
        if (project == null) return;

        project.DefaultModeId = (_projectDefaultModeCombo.SelectedItem as ProjectModeComboItem)?.ModeId ?? "";
        project.ContextNotes = _projectContextNotesTextBox.Text;
    }

    private void RefreshProjectDocumentsList(ProjectConfig? project)
    {
        _projectDocumentsList.Items.Clear();
        if (project?.Documents == null) return;

        foreach (var document in project.Documents.OrderByDescending(document => document.ImportedAt))
        {
            _projectDocumentsList.Items.Add(new ProjectDocumentListItem(document));
        }

        UpdateProjectButtons();
    }

    private void SelectProjectByName(string projectName)
    {
        for (var i = 0; i < _projectsList.Items.Count; i++)
        {
            if (_projectsList.Items[i] is ProjectListItem item &&
                string.Equals(item.Project.Name, projectName, StringComparison.OrdinalIgnoreCase))
            {
                _projectsList.SelectedIndex = i;
                return;
            }
        }
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
                _apiKeyLink.Text = $"Get API key ({host})";
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
        UpdateSelectedProjectFromDetails();

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

    private sealed class FooterBarPanel : Panel
    {
        public FooterBarPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(BorderSoftColor);
            e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
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
            BackColor = SurfaceColor;
            ForeColor = PrimaryTextColor;
            Font = new Font("Segoe UI", 9.6f, FontStyle.Regular);
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
                ? SurfaceAltColor
                : _pressed
                    ? BorderSoftColor
                    : _hovering || Focused
                        ? HoverColor
                        : SurfaceColor;
            var borderColor = Focused ? BorderColor : fillColor;
            var textColor = Selected ? PrimaryTextColor : MutedTextColor;

            using (var path = RoundedRect(bounds, 6))
            using (var fill = new SolidBrush(fillColor))
            using (var border = new Pen(borderColor, Focused ? 1.2f : 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            if (Selected)
            {
                using var accent = new SolidBrush(AccentColor);
                g.FillRectangle(accent, 0, 9, 3, Math.Max(10, Height - 18));
            }

            using var font = new Font("Segoe UI", 9.6f, Selected ? FontStyle.Bold : FontStyle.Regular);
            var textBounds = new Rectangle(14, 0, Math.Max(1, Width - 20), Height);
            TextRenderer.DrawText(
                g,
                Text,
                font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
            Height = 34;
            TabStop = true;

            _dropDown.Padding = Padding.Empty;
            _dropDown.Margin = Padding.Empty;
            _dropDown.AutoClose = true;
            _dropDown.BackColor = FieldColor;
            _dropDown.DropShadowEnabled = true;

            _listBox.BorderStyle = BorderStyle.None;
            _listBox.DrawMode = DrawMode.OwnerDrawFixed;
            _listBox.ItemHeight = 30;
            _listBox.BackColor = FieldColor;
            _listBox.ForeColor = PrimaryTextColor;
            _listBox.Font = new Font("Segoe UI", 9.6f);
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
                ? BorderSoftColor
                : _hovering || Focused
                    ? HoverColor
                    : FieldColor;

            using (var path = RoundedRect(bounds, 6))
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
            using var arrowBrush = new SolidBrush(MutedTextColor);
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
            using var fill = new SolidBrush(selected ? Color.FromArgb(232, 242, 253) : FieldColor);
            e.Graphics.FillRectangle(fill, e.Bounds);

            var color = PrimaryTextColor;
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
                FormatMs(entry.TotalMs));

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

    private class ProjectModeComboItem
    {
        public string ModeId { get; }
        private readonly string _label;

        public ProjectModeComboItem(AIMode? mode)
        {
            ModeId = mode?.Id ?? "";
            _label = mode == null ? "No project default" : mode.Name;
        }

        public override string ToString() => _label;
    }

    private class ProjectDocumentListItem
    {
        public ProjectContextDocument Document { get; }

        public ProjectDocumentListItem(ProjectContextDocument document)
        {
            Document = document;
        }

        public override string ToString()
        {
            var imported = Document.ImportedAt == default ? "" : $" - {Document.ImportedAt:g}";
            return $"{Document.FileName} ({Document.CharacterCount:N0} chars){imported}";
        }
    }
}
