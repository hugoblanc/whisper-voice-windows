using WhisperVoice.Api;
using WhisperVoice.Logging;
using WhisperVoice.Processing;

namespace WhisperVoice.Config;

public class PreferencesWindow : Form
{
    private TabControl _tabControl = null!;

    // General tab
    private ComboBox _providerCombo = null!;
    private TextBox _apiKeyTextBox = null!;
    private LinkLabel _apiKeyLink = null!;
    private Button _testConnectionButton = null!;
    private Label _connectionStatusLabel = null!;
    private ComboBox _audioCaptureModeCombo = null!;
    private Label _audioCaptureModeDescriptionLabel = null!;

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
    private DataGridView _autoModeRulesGrid = null!;
    private Button _editAutoModeRuleButton = null!;
    private Button _deleteAutoModeRuleButton = null!;
    private readonly List<AutoModeRuleConfig> _autoModeRules = new();

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
    private bool _connectionTested;
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
        ClientSize = new Size(760, 640);
        MinimumSize = new Size(640, 540);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        _tabControl = new TabControl
        {
            Location = new Point(10, 10),
            Size = new Size(ClientSize.Width - 20, ClientSize.Height - 72),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        // Create tabs
        var generalTab = CreateGeneralTab();
        var shortcutsTab = CreateShortcutsTab();
        var modesTab = CreateModesTab();
        var autoModeTab = CreateAutoModeTab();
        var journalTab = CreateJournalTab();
        var logsTab = CreateLogsTab();

        _tabControl.TabPages.Add(generalTab);
        _tabControl.TabPages.Add(shortcutsTab);
        _tabControl.TabPages.Add(modesTab);
        _tabControl.TabPages.Add(autoModeTab);
        _tabControl.TabPages.Add(journalTab);
        _tabControl.TabPages.Add(logsTab);

        _footerPanel = new FlowLayoutPanel
        {
            Height = 46,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 10, 8)
        };

        _saveButton = new Button
        {
            Text = "Save",
            Size = new Size(104, 32),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _saveButton.FlatAppearance.BorderSize = 0;
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(104, 32),
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.Click += (_, _) => Close();

        _footerPanel.Controls.Add(_cancelButton);
        _footerPanel.Controls.Add(_saveButton);

        Controls.Add(_tabControl);
        Controls.Add(_footerPanel);
    }

    private TabPage CreateGeneralTab()
    {
        var tab = new TabPage("General");
        tab.Padding = new Padding(12);

        // Provider selection
        var providerLabel = new Label
        {
            Text = "Transcription Provider:",
            Location = new Point(15, 20),
            AutoSize = true
        };

        _providerCombo = new ComboBox
        {
            Location = new Point(15, 40),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
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
            Location = new Point(15, 80),
            AutoSize = true
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(15, 100),
            Size = new Size(420, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            UseSystemPasswordChar = true,
            PlaceholderText = "sk-..."
        };
        _apiKeyTextBox.TextChanged += (_, _) => ResetConnectionStatus();

        _apiKeyLink = new LinkLabel
        {
            Text = "Get your API key from platform.openai.com",
            Location = new Point(15, 128),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _apiKeyLink.Click += ApiKeyLink_Click;

        // Test Connection
        _testConnectionButton = new Button
        {
            Text = "Test Connection",
            Location = new Point(15, 165),
            Size = new Size(120, 30),
            FlatStyle = FlatStyle.Flat
        };
        _testConnectionButton.Click += TestConnectionButton_Click;

        _connectionStatusLabel = new Label
        {
            Text = "",
            Location = new Point(145, 172),
            Size = new Size(290, 20),
            AutoSize = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var audioCaptureLabel = new Label
        {
            Text = "Recording latency mode:",
            Location = new Point(15, 220),
            AutoSize = true
        };

        _audioCaptureModeCombo = new ComboBox
        {
            Location = new Point(15, 242),
            Size = new Size(260, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
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
            Location = new Point(15, 276),
            Size = new Size(680, 58),
            AutoSize = false,
            ForeColor = Color.Gray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        tab.Controls.AddRange(new Control[]
        {
            providerLabel, _providerCombo,
            apiKeyLabel, _apiKeyTextBox, _apiKeyLink,
            _testConnectionButton, _connectionStatusLabel,
            audioCaptureLabel, _audioCaptureModeCombo, _audioCaptureModeDescriptionLabel
        });

        return tab;
    }

    private TabPage CreateShortcutsTab()
    {
        var tab = new TabPage("Shortcuts");
        tab.Padding = new Padding(12);

        // Toggle shortcut
        var shortcutLabel = new Label
        {
            Text = "Toggle Shortcut (start/stop recording):",
            Location = new Point(15, 20),
            AutoSize = true
        };

        _shortcutCombo = new ComboBox
        {
            Location = new Point(15, 40),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
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
            Location = new Point(15, 85),
            AutoSize = true
        };

        _pttCombo = new ComboBox
        {
            Location = new Point(15, 105),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
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
            Location = new Point(15, 155),
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

    private TabPage CreateModesTab()
    {
        var tab = new TabPage("Modes");
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

    private TabPage CreateAutoModeTab()
    {
        var tab = new TabPage("Auto Mode");
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        _autoModeEnabledCheckBox = new CheckBox
        {
            Text = "Automatically select mode from the active app or window title",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
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
        root.Controls.Add(_autoModeRulesGrid, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        root.Controls.Add(hintLabel, 0, 3);

        tab.Controls.Add(root);
        return tab;
    }

    private TabPage CreateLogsTab()
    {
        var tab = new TabPage("Logs");
        tab.Padding = new Padding(12);

        _autoScrollCheckBox = new CheckBox
        {
            Text = "Auto-scroll",
            Location = new Point(15, 15),
            AutoSize = true,
            Checked = true
        };

        _logTextBox = new TextBox
        {
            Location = new Point(15, 40),
            Size = new Size(420, 250),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 8.5F),
            WordWrap = false
        };

        var clearLogsButton = new Button
        {
            Text = "Clear Logs",
            Location = new Point(15, 300),
            Size = new Size(90, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            FlatStyle = FlatStyle.Flat
        };
        clearLogsButton.Click += ClearLogsButton_Click;

        var openLogFolderButton = new Button
        {
            Text = "Open Log Folder",
            Location = new Point(115, 300),
            Size = new Size(110, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            FlatStyle = FlatStyle.Flat
        };
        openLogFolderButton.Click += (_, _) => Logger.OpenLogFolder();

        tab.Controls.AddRange(new Control[]
        {
            _autoScrollCheckBox,
            _logTextBox,
            clearLogsButton,
            openLogFolderButton
        });

        return tab;
    }

    private TabPage CreateJournalTab()
    {
        var tab = new TabPage("Journal");
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
        _journalGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Paste", HeaderText = "Paste", FillWeight = 65 });
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

        // Load custom modes
        _customModes.Clear();
        _customModes.AddRange((_originalConfig.CustomModes ?? new List<CustomModeConfig>())
            .Select(mode => mode.Clone()));
        RefreshCustomModesList();

        _autoModeEnabledCheckBox.Checked = _originalConfig.AutoModeEnabled;
        _autoModeRules.Clear();
        _autoModeRules.AddRange((_originalConfig.AutoModeRules ?? AutoModeRuleConfig.CreateDefaults())
            .Select(rule => rule.Clone()));
        RefreshAutoModeRulesGrid();

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
        _connectionTested = false;
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

            _connectionTested = true;
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
            _connectionTested = true;
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
            if (_tabControl.SelectedTab?.Text == "Logs")
            {
                RefreshLogs();
            }
            else if (_tabControl.SelectedTab?.Text == "Journal")
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
            CustomModes = BuildCustomModesForSave(),
            DisabledBuiltInModeIds = _originalConfig.DisabledBuiltInModeIds?.ToList() ?? new List<string>(),
            AudioCaptureMode = audioCaptureMode,
            AutoModeEnabled = _autoModeEnabledCheckBox.Checked,
            AutoModeRules = BuildAutoModeRulesForSave()
        };

        try
        {
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
                FormatStepMs(entry, "paste_result"));

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
            $"Paste: {(entry.Pasted.HasValue ? (entry.Pasted.Value ? "ok" : "warning") : "-")}"
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

    private static string FormatStepMs(RecordingJournalEntry entry, string stepName)
    {
        var step = entry.Steps.FirstOrDefault(step => string.Equals(step.Name, stepName, StringComparison.OrdinalIgnoreCase));
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
}
