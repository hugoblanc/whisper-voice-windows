using WhisperVoice.Api;

namespace WhisperVoice.Config;

public class SetupWizard : Form
{
    private ComboBox _providerCombo = null!;
    private TextBox _apiKeyTextBox = null!;
    private LinkLabel _apiKeyLink = null!;
    private ComboBox _shortcutCombo = null!;
    private ComboBox _pttCombo = null!;
    private CheckBox _autoStartCheckBox = null!;
    private Button _saveButton = null!;
    private Label _statusLabel = null!;

    public AppConfig? Result { get; private set; }

    public SetupWizard()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        Text = "Whisper Voice - Setup";
        Size = new Size(450, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        var titleLabel = new Label
        {
            Text = "Whisper Voice Setup",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var subtitleLabel = new Label
        {
            Text = "Configure your voice transcription settings",
            ForeColor = Color.Gray,
            Location = new Point(20, 45),
            AutoSize = true
        };

        // Provider selection
        var providerLabel = new Label
        {
            Text = "Transcription Provider:",
            Location = new Point(20, 85),
            AutoSize = true
        };

        _providerCombo = new ComboBox
        {
            Location = new Point(20, 105),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        // Populate providers from factory
        foreach (var provider in TranscriptionProviderFactory.GetAvailableProviders())
        {
            _providerCombo.Items.Add(new ProviderComboItem(provider));
        }
        _providerCombo.SelectedIndex = 0;
        _providerCombo.SelectedIndexChanged += ProviderCombo_Changed;

        // API Key
        var apiKeyLabel = new Label
        {
            Text = "API Key:",
            Location = new Point(20, 140),
            AutoSize = true
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(20, 160),
            Size = new Size(390, 25),
            UseSystemPasswordChar = true,
            PlaceholderText = "sk-..."
        };

        _apiKeyLink = new LinkLabel
        {
            Text = "Get your API key from platform.openai.com",
            Location = new Point(20, 188),
            AutoSize = true
        };
        _apiKeyLink.Click += ApiKeyLink_Click;

        // Toggle shortcut
        var shortcutLabel = new Label
        {
            Text = "Toggle Shortcut (start/stop recording):",
            Location = new Point(20, 220),
            AutoSize = true
        };

        _shortcutCombo = new ComboBox
        {
            Location = new Point(20, 240),
            Size = new Size(180, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _shortcutCombo.Items.AddRange(new object[]
        {
            "Ctrl+Shift+Space (recommended)",
            "Alt+Space",
            "Ctrl+Space",
            "Win+Shift+Space"
        });
        _shortcutCombo.SelectedIndex = 0;

        // PTT key
        var pttLabel = new Label
        {
            Text = "Push-to-Talk Key (hold to record):",
            Location = new Point(230, 220),
            AutoSize = true
        };

        _pttCombo = new ComboBox
        {
            Location = new Point(230, 240),
            Size = new Size(180, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _pttCombo.Items.AddRange(new object[] { "F1", "F2", "F3 (recommended)", "F4", "F5", "F6" });
        _pttCombo.SelectedIndex = 2;

        // Auto-start
        _autoStartCheckBox = new CheckBox
        {
            Text = "Start automatically when Windows starts",
            Location = new Point(20, 280),
            AutoSize = true
        };

        // Status label
        _statusLabel = new Label
        {
            Text = "",
            ForeColor = Color.Red,
            Location = new Point(20, 310),
            Size = new Size(390, 20)
        };

        // Save button
        _saveButton = new Button
        {
            Text = "Save && Start",
            Location = new Point(280, 340),
            Size = new Size(130, 35),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _saveButton.FlatAppearance.BorderSize = 0;
        _saveButton.Click += SaveButton_Click;

        // Update UI for initial provider selection
        UpdateProviderUI();

        Controls.AddRange(new Control[]
        {
            titleLabel, subtitleLabel,
            providerLabel, _providerCombo,
            apiKeyLabel, _apiKeyTextBox, _apiKeyLink,
            shortcutLabel, _shortcutCombo,
            pttLabel, _pttCombo,
            _autoStartCheckBox,
            _statusLabel,
            _saveButton
        });
    }

    private void ProviderCombo_Changed(object? sender, EventArgs e)
    {
        UpdateProviderUI();
    }

    private void UpdateProviderUI()
    {
        if (_providerCombo.SelectedItem is ProviderComboItem item)
        {
            var host = new Uri(item.Info.ApiKeyHelpUrl).Host;
            _apiKeyLink.Text = $"Get your API key from {host}";
            _apiKeyLink.Tag = item.Info.ApiKeyHelpUrl;

            // Update placeholder based on provider
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

    private class ProviderComboItem
    {
        public ProviderInfo Info { get; }
        public ProviderComboItem(ProviderInfo info) => Info = info;
        public override string ToString() => Info.DisplayName;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var apiKey = _apiKeyTextBox.Text.Trim();
        var selectedProvider = (_providerCombo.SelectedItem as ProviderComboItem)?.Info;

        if (selectedProvider == null)
        {
            _statusLabel.Text = "Please select a provider.";
            return;
        }

        // Provider-specific validation
        if (!TranscriptionProviderFactory.ValidateApiKey(selectedProvider.Id, apiKey, out var errorMessage))
        {
            _statusLabel.Text = errorMessage ?? "Invalid API key.";
            return;
        }

        // Parse shortcut
        uint shortcutModifiers = _shortcutCombo.SelectedIndex switch
        {
            0 => 0x0006, // MOD_CONTROL | MOD_SHIFT (Ctrl+Shift+Space - recommended, no conflicts)
            1 => 0x0001, // MOD_ALT (Alt+Space - may conflict with Windows system menu)
            2 => 0x0002, // MOD_CONTROL (Ctrl+Space - may conflict with IME)
            3 => 0x000C, // MOD_WIN | MOD_SHIFT
            _ => 0x0006
        };

        // Parse PTT key
        uint pttKeyCode = _pttCombo.SelectedIndex switch
        {
            0 => 0x70, // VK_F1
            1 => 0x71, // VK_F2
            2 => 0x72, // VK_F3
            3 => 0x73, // VK_F4
            4 => 0x74, // VK_F5
            5 => 0x75, // VK_F6
            _ => 0x72
        };

        Result = new AppConfig
        {
            Provider = selectedProvider.Id,
            ApiKey = apiKey,
            ShortcutModifiers = shortcutModifiers,
            ShortcutKeyCode = 0x20, // VK_SPACE
            PushToTalkKeyCode = pttKeyCode
        };

        try
        {
            Result.Save();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed to save config: {ex.Message}";
            return;
        }

        // Set auto-start if checked
        if (_autoStartCheckBox.Checked)
        {
            try
            {
                SetAutoStart(true);
            }
            catch
            {
                // Ignore auto-start errors
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void SetAutoStart(bool enable)
    {
        var exePath = Application.ExecutablePath;
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

        if (key == null) return;

        if (enable)
        {
            key.SetValue("WhisperVoice", $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue("WhisperVoice", false);
        }
    }
}
