using WhisperVoice.History;

namespace WhisperVoice.Config;

public class ProjectPickerDialog : Form
{
    private readonly ListBox _projectsList;
    private readonly TextBox _newProjectTextBox;
    private readonly List<ProjectConfig> _projects;

    public ProjectConfig? SelectedProject { get; private set; }

    public ProjectPickerDialog(ProjectConfig? currentProject)
    {
        _projects = ProjectStore.LoadProjects()
            .Where(project => !project.Archived)
            .OrderBy(project => project.Name)
            .ToList();

        Text = "Choose Project";
        ClientSize = new Size(420, 390);
        MinimumSize = new Size(360, 320);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);

        var listLabel = new Label
        {
            Text = "Project:",
            Location = new Point(16, 16),
            AutoSize = true
        };

        _projectsList = new ListBox
        {
            Location = new Point(16, 44),
            Size = new Size(ClientSize.Width - 32, 190),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            IntegralHeight = false
        };
        _projectsList.Items.Add(new ProjectListItem(null));
        foreach (var project in _projects)
        {
            _projectsList.Items.Add(new ProjectListItem(project));
        }

        SelectCurrent(currentProject);

        var newLabel = new Label
        {
            Text = "Or create new:",
            Location = new Point(16, ClientSize.Height - 136),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        _newProjectTextBox = new TextBox
        {
            Location = new Point(16, ClientSize.Height - 108),
            Size = new Size(ClientSize.Width - 32, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "Project name"
        };

        var saveButton = new Button
        {
            Text = "Choose",
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
            listLabel,
            _projectsList,
            newLabel,
            _newProjectTextBox,
            saveButton,
            cancelButton
        });

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void SelectCurrent(ProjectConfig? currentProject)
    {
        _projectsList.SelectedIndex = 0;
        if (currentProject == null) return;

        for (var i = 0; i < _projectsList.Items.Count; i++)
        {
            if (_projectsList.Items[i] is ProjectListItem item &&
                item.Project != null &&
                string.Equals(item.Project.Id, currentProject.Id, StringComparison.OrdinalIgnoreCase))
            {
                _projectsList.SelectedIndex = i;
                return;
            }
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var newName = _newProjectTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            SelectedProject = ProjectStore.GetOrCreateProject(newName);
            return;
        }

        SelectedProject = (_projectsList.SelectedItem as ProjectListItem)?.Project;
    }

    private sealed class ProjectListItem
    {
        public ProjectConfig? Project { get; }

        public ProjectListItem(ProjectConfig? project)
        {
            Project = project;
        }

        public override string ToString() => Project?.Name ?? "(untagged)";
    }
}
