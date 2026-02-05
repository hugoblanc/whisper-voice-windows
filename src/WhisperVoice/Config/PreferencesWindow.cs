using WhisperVoice.Api;
using WhisperVoice.Logging;

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

    // Shortcuts tab
    private ComboBox _shortcutCombo = null!;
    private ComboBox _pttCombo = null!;

    // Logs tab
    private TextBox _logTextBox = null!;
    private CheckBox _autoScrollCheckBox = null!;
    private System.Windows.Forms.Timer _logRefreshTimer = null!;

    // Footer
    private Button _saveButton = null!;
    private Button _cancelButton = null!;

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
        Size = new Size(500, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        _tabControl = new TabControl
        {
            Location = new Point(10, 10),
            Size = new Size(465, 380)
        };

        // Create tabs
        var generalTab = CreateGeneralTab();
        var shortcutsTab = CreateShortcutsTab();
        var logsTab = CreateLogsTab();

        _tabControl.TabPages.Add(generalTab);
        _tabControl.TabPages.Add(shortcutsTab);
        _tabControl.TabPages.Add(logsTab);

        // Footer buttons
        _saveButton = new Button
        {
            Text = "Save",
            Location = new Point(295, 400),
            Size = new Size(85, 30),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _saveButton.FlatAppearance.BorderSize = 0;
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(390, 400),
            Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.Click += (_, _) => Close();

        Controls.Add(_tabControl);
        Controls.Add(_saveButton);
        Controls.Add(_cancelButton);
    }

    private TabPage CreateGeneralTab()
    {
        var tab = new TabPage("General");

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
            UseSystemPasswordChar = true,
            PlaceholderText = "sk-..."
        };
        _apiKeyTextBox.TextChanged += (_, _) => ResetConnectionStatus();

        _apiKeyLink = new LinkLabel
        {
            Text = "Get your API key from platform.openai.com",
            Location = new Point(15, 128),
            AutoSize = true
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
            AutoSize = false
        };

        tab.Controls.AddRange(new Control[]
        {
            providerLabel, _providerCombo,
            apiKeyLabel, _apiKeyTextBox, _apiKeyLink,
            _testConnectionButton, _connectionStatusLabel
        });

        return tab;
    }

    private TabPage CreateShortcutsTab()
    {
        var tab = new TabPage("Shortcuts");

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

    private TabPage CreateLogsTab()
    {
        var tab = new TabPage("Logs");

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
            FlatStyle = FlatStyle.Flat
        };
        clearLogsButton.Click += ClearLogsButton_Click;

        var openLogFolderButton = new Button
        {
            Text = "Open Log Folder",
            Location = new Point(115, 300),
            Size = new Size(110, 28),
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

        // Update UI for selected provider
        UpdateProviderUI();

        // Load initial logs
        RefreshLogs();
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
            var host = new Uri(item.Info.ApiKeyHelpUrl).Host;
            _apiKeyLink.Text = $"Get your API key from {host}";
            _apiKeyLink.Tag = item.Info.ApiKeyHelpUrl;

            _apiKeyTextBox.PlaceholderText = item.Info.Id == "openai" ? "sk-..." : "Enter API key";
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
            if (_tabControl.SelectedIndex == 2)
            {
                RefreshLogs();
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

        var newConfig = new AppConfig
        {
            Provider = selectedProvider.Id,
            ApiKey = apiKey,
            ShortcutModifiers = shortcutModifiers,
            ShortcutKeyCode = 0x20, // VK_SPACE
            PushToTalkKeyCode = pttKeyCode
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
}
