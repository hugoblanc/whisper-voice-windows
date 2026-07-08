using WhisperVoice.Processing;

namespace WhisperVoice.Config;

public class AutoModeRuleEditorDialog : Form
{
    private readonly TextBox _nameTextBox;
    private readonly CheckBox _enabledCheckBox;
    private readonly TextBox _processTextBox;
    private readonly TextBox _titleTextBox;
    private readonly ComboBox _modeCombo;
    private readonly string? _originalId;

    public AutoModeRuleConfig Rule { get; private set; }

    public AutoModeRuleEditorDialog(AutoModeRuleConfig? rule, IEnumerable<AIMode> availableModes)
    {
        _originalId = rule?.Id;
        Rule = rule?.Clone() ?? new AutoModeRuleConfig { Enabled = true };

        Text = rule == null ? "Add Auto Mode Rule" : "Edit Auto Mode Rule";
        ClientSize = new Size(560, 320);
        MinimumSize = new Size(500, 300);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = true;
        Font = new Font("Segoe UI", 9F);

        var nameLabel = new Label
        {
            Text = "Name:",
            Location = new Point(16, 18),
            AutoSize = true
        };

        _nameTextBox = new TextBox
        {
            Text = Rule.Name,
            Location = new Point(16, 40),
            Size = new Size(ClientSize.Width - 32, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "e.g. Gmail in Chrome -> Email"
        };

        _enabledCheckBox = new CheckBox
        {
            Text = "Enabled",
            Checked = Rule.Enabled,
            Location = new Point(16, 74),
            AutoSize = true
        };

        var processLabel = new Label
        {
            Text = "Application process:",
            Location = new Point(16, 110),
            AutoSize = true
        };

        _processTextBox = new TextBox
        {
            Text = Rule.ProcessName,
            Location = new Point(16, 132),
            Size = new Size(ClientSize.Width - 32, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "e.g. chrome, Slack, Code"
        };

        var titleLabel = new Label
        {
            Text = "Window title contains:",
            Location = new Point(16, 168),
            AutoSize = true
        };

        _titleTextBox = new TextBox
        {
            Text = Rule.WindowTitleContains,
            Location = new Point(16, 190),
            Size = new Size(ClientSize.Width - 32, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "e.g. Gmail"
        };

        var modeLabel = new Label
        {
            Text = "Mode:",
            Location = new Point(16, 226),
            AutoSize = true
        };

        _modeCombo = new ComboBox
        {
            Location = new Point(16, 248),
            Size = new Size(240, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        foreach (var mode in availableModes)
        {
            _modeCombo.Items.Add(new ModeComboItem(mode));
        }

        SelectCurrentMode();

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(ClientSize.Width - 200, ClientSize.Height - 42),
            Size = new Size(85, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += SaveButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(ClientSize.Width - 105, ClientSize.Height - 42),
            Size = new Size(85, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat
        };

        Controls.AddRange(new Control[]
        {
            nameLabel,
            _nameTextBox,
            _enabledCheckBox,
            processLabel,
            _processTextBox,
            titleLabel,
            _titleTextBox,
            modeLabel,
            _modeCombo,
            saveButton,
            cancelButton
        });

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void SelectCurrentMode()
    {
        for (var i = 0; i < _modeCombo.Items.Count; i++)
        {
            if (_modeCombo.Items[i] is ModeComboItem item &&
                string.Equals(item.Mode.Id, Rule.ModeId, StringComparison.OrdinalIgnoreCase))
            {
                _modeCombo.SelectedIndex = i;
                return;
            }
        }

        if (_modeCombo.Items.Count > 0)
        {
            _modeCombo.SelectedIndex = 0;
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var processName = _processTextBox.Text.Trim();
        var titleContains = _titleTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(titleContains))
        {
            MessageBox.Show("Please enter an application process, a window title match, or both.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (_modeCombo.SelectedItem is not ModeComboItem selectedMode)
        {
            MessageBox.Show("Please select a mode.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var name = _nameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = BuildName(processName, titleContains, selectedMode.Mode.Name);
        }

        Rule = new AutoModeRuleConfig
        {
            Id = string.IsNullOrWhiteSpace(_originalId) ? Guid.NewGuid().ToString("N") : _originalId,
            Name = name,
            Enabled = _enabledCheckBox.Checked,
            ProcessName = processName,
            WindowTitleContains = titleContains,
            ModeId = selectedMode.Mode.Id
        };
    }

    private static string BuildName(string processName, string titleContains, string modeName)
    {
        var target = string.IsNullOrWhiteSpace(processName) ? "Any app" : processName;
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            target += $" / {titleContains}";
        }

        return $"{target} -> {modeName}";
    }

    private sealed class ModeComboItem
    {
        public AIMode Mode { get; }

        public ModeComboItem(AIMode mode) => Mode = mode;

        public override string ToString() => Mode.Name;
    }
}
