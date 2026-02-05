namespace WhisperVoice.Config;

public class SetupWizard : Form
{
    private TextBox _apiKeyTextBox = null!;
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
        Size = new Size(450, 380);
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

        // API Key
        var apiKeyLabel = new Label
        {
            Text = "OpenAI API Key:",
            Location = new Point(20, 85),
            AutoSize = true
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(20, 105),
            Size = new Size(390, 25),
            UseSystemPasswordChar = true,
            PlaceholderText = "sk-..."
        };

        var apiKeyLink = new LinkLabel
        {
            Text = "Get your API key from platform.openai.com",
            Location = new Point(20, 133),
            AutoSize = true
        };
        apiKeyLink.Click += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://platform.openai.com/api-keys",
                UseShellExecute = true
            });
        };

        // Toggle shortcut
        var shortcutLabel = new Label
        {
            Text = "Toggle Shortcut (start/stop recording):",
            Location = new Point(20, 165),
            AutoSize = true
        };

        _shortcutCombo = new ComboBox
        {
            Location = new Point(20, 185),
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
            Location = new Point(230, 165),
            AutoSize = true
        };

        _pttCombo = new ComboBox
        {
            Location = new Point(230, 185),
            Size = new Size(180, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _pttCombo.Items.AddRange(new object[] { "F1", "F2", "F3 (recommended)", "F4", "F5", "F6" });
        _pttCombo.SelectedIndex = 2;

        // Auto-start
        _autoStartCheckBox = new CheckBox
        {
            Text = "Start automatically when Windows starts",
            Location = new Point(20, 225),
            AutoSize = true
        };

        // Status label
        _statusLabel = new Label
        {
            Text = "",
            ForeColor = Color.Red,
            Location = new Point(20, 260),
            Size = new Size(390, 20)
        };

        // Save button
        _saveButton = new Button
        {
            Text = "Save && Start",
            Location = new Point(280, 290),
            Size = new Size(130, 35),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _saveButton.FlatAppearance.BorderSize = 0;
        _saveButton.Click += SaveButton_Click;

        Controls.AddRange(new Control[]
        {
            titleLabel, subtitleLabel,
            apiKeyLabel, _apiKeyTextBox, apiKeyLink,
            shortcutLabel, _shortcutCombo,
            pttLabel, _pttCombo,
            _autoStartCheckBox,
            _statusLabel,
            _saveButton
        });
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var apiKey = _apiKeyTextBox.Text.Trim();

        if (string.IsNullOrEmpty(apiKey))
        {
            _statusLabel.Text = "Please enter your API key.";
            return;
        }

        if (!apiKey.StartsWith("sk-") && !apiKey.StartsWith("sk-proj-"))
        {
            _statusLabel.Text = "Invalid API key format. Should start with 'sk-' or 'sk-proj-'.";
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
