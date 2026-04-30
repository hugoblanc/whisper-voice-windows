namespace WhisperVoice.Config;

public class CustomModeEditorDialog : Form
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _promptTextBox;
    private readonly CheckBox _enabledCheckBox;
    private readonly string? _originalId;
    private readonly IEnumerable<string> _existingIds;

    public CustomModeConfig Mode { get; private set; }

    public CustomModeEditorDialog(CustomModeConfig? mode, IEnumerable<string> existingIds)
    {
        _originalId = mode?.Id;
        _existingIds = existingIds;
        Mode = mode?.Clone() ?? new CustomModeConfig();

        Text = mode == null ? "Add Custom Mode" : "Edit Custom Mode";
        ClientSize = new Size(620, 460);
        MinimumSize = new Size(520, 380);
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
            Text = Mode.Name,
            Location = new Point(16, 40),
            Size = new Size(ClientSize.Width - 32, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "e.g. Email, Code Review, Meeting Notes"
        };

        _enabledCheckBox = new CheckBox
        {
            Text = "Enabled",
            Checked = Mode.Enabled,
            Location = new Point(16, 74),
            AutoSize = true
        };

        var promptLabel = new Label
        {
            Text = "System prompt:",
            Location = new Point(16, 108),
            AutoSize = true
        };

        _promptTextBox = new TextBox
        {
            Text = Mode.Prompt,
            Location = new Point(16, 130),
            Size = new Size(ClientSize.Width - 32, ClientSize.Height - 188),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            AcceptsTab = true,
            Font = new Font("Consolas", 9F),
            PlaceholderText = "Describe exactly how this mode should transform the transcription."
        };

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
            promptLabel,
            _promptTextBox,
            saveButton,
            cancelButton
        });

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var name = _nameTextBox.Text.Trim();
        var prompt = _promptTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a mode name.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            MessageBox.Show("Please enter a system prompt.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var id = string.IsNullOrWhiteSpace(_originalId)
            ? CustomModeConfig.CreateUniqueId(name, _existingIds)
            : _originalId;

        Mode = new CustomModeConfig
        {
            Id = id,
            Name = name,
            Prompt = prompt,
            Enabled = _enabledCheckBox.Checked,
            Icon = "star"
        };
    }
}
