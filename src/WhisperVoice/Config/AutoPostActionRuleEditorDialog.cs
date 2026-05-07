namespace WhisperVoice.Config;

public class AutoPostActionRuleEditorDialog : Form
{
    private readonly TextBox _nameTextBox;
    private readonly CheckBox _enabledCheckBox;
    private readonly TextBox _processTextBox;
    private readonly TextBox _titleTextBox;
    private readonly ComboBox _actionCombo;
    private readonly string? _originalId;

    public AutoPostActionRuleConfig Rule { get; private set; }

    public AutoPostActionRuleEditorDialog(AutoPostActionRuleConfig? rule, IEnumerable<PostActionConfig> availableActions)
    {
        _originalId = rule?.Id;
        Rule = rule?.Clone() ?? new AutoPostActionRuleConfig { Enabled = true };

        Text = rule == null ? "Add Auto Action Rule" : "Edit Auto Action Rule";
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
            PlaceholderText = "e.g. Slack -> Paste + Enter"
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

        var actionLabel = new Label
        {
            Text = "Action:",
            Location = new Point(16, 226),
            AutoSize = true
        };

        _actionCombo = new ComboBox
        {
            Location = new Point(16, 248),
            Size = new Size(280, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        foreach (var action in availableActions)
        {
            _actionCombo.Items.Add(new ActionComboItem(action));
        }

        SelectCurrentAction();

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
            actionLabel,
            _actionCombo,
            saveButton,
            cancelButton
        });

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void SelectCurrentAction()
    {
        for (var i = 0; i < _actionCombo.Items.Count; i++)
        {
            if (_actionCombo.Items[i] is ActionComboItem item &&
                string.Equals(item.Action.Id, Rule.ActionId, StringComparison.OrdinalIgnoreCase))
            {
                _actionCombo.SelectedIndex = i;
                return;
            }
        }

        if (_actionCombo.Items.Count > 0)
        {
            _actionCombo.SelectedIndex = 0;
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

        if (_actionCombo.SelectedItem is not ActionComboItem selectedAction)
        {
            MessageBox.Show("Please select an action.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var name = _nameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = BuildName(processName, titleContains, selectedAction.Action.Label);
        }

        Rule = new AutoPostActionRuleConfig
        {
            Id = string.IsNullOrWhiteSpace(_originalId) ? Guid.NewGuid().ToString("N") : _originalId,
            Name = name,
            Enabled = _enabledCheckBox.Checked,
            ProcessName = processName,
            WindowTitleContains = titleContains,
            ActionId = selectedAction.Action.Id
        };
    }

    private static string BuildName(string processName, string titleContains, string actionName)
    {
        var target = string.IsNullOrWhiteSpace(processName) ? "Any app" : processName;
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            target += $" / {titleContains}";
        }

        return $"{target} -> {actionName}";
    }

    private sealed class ActionComboItem
    {
        public PostActionConfig Action { get; }

        public ActionComboItem(PostActionConfig action) => Action = action;

        public override string ToString() => Action.Label;
    }
}
