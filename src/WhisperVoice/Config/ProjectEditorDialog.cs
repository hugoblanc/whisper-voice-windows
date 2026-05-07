namespace WhisperVoice.Config;

public class ProjectEditorDialog : Form
{
    private readonly TextBox _nameTextBox;

    public string ProjectName { get; private set; } = "";

    public ProjectEditorDialog(string? projectName)
    {
        ProjectName = projectName?.Trim() ?? "";

        Text = string.IsNullOrWhiteSpace(ProjectName) ? "Add Project" : "Rename Project";
        ClientSize = new Size(420, 160);
        MinimumSize = new Size(360, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);

        var nameLabel = new Label
        {
            Text = "Project name:",
            Location = new Point(16, 18),
            AutoSize = true
        };

        _nameTextBox = new TextBox
        {
            Text = ProjectName,
            Location = new Point(16, 48),
            Size = new Size(ClientSize.Width - 32, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "e.g. Client project, Product roadmap, Personal"
        };

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(ClientSize.Width - 214, ClientSize.Height - 50),
            Size = new Size(94, 34),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.OK
        };
        saveButton.Click += SaveButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(ClientSize.Width - 110, ClientSize.Height - 50),
            Size = new Size(94, 34),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[]
        {
            nameLabel,
            _nameTextBox,
            saveButton,
            cancelButton
        });

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _nameTextBox.Focus();
        _nameTextBox.SelectAll();
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        ProjectName = _nameTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(ProjectName)) return;

        MessageBox.Show("Please enter a project name.", "Validation Error",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        DialogResult = DialogResult.None;
    }
}
