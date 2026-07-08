using WhisperVoice.Logging;
using WhisperVoice.Config;

namespace WhisperVoice.History;

/// <summary>
/// Window to view, search, and manage transcription history
/// </summary>
public class HistoryWindow : Form
{
    private DataGridView _dataGrid = null!;
    private TextBox _searchBox = null!;
    private ComboBox _projectFilterCombo = null!;
    private Button _copyButton = null!;
    private Button _deleteButton = null!;
    private Button _tagProjectButton = null!;
    private Button _clearAllButton = null!;
    private List<ProjectConfig> _projects = new();
    private List<TranscriptionEntry> _allEntries = null!;
    private List<TranscriptionEntry> _filteredEntries = null!;

    public HistoryWindow()
    {
        Text = "Transcription History";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 400);

        InitializeComponents();
        LoadHistory();
    }

    private void InitializeComponents()
    {
        // Search box
        var searchLabel = new Label
        {
            Text = "Search:",
            Location = new Point(10, 15),
            AutoSize = true
        };

        _searchBox = new TextBox
        {
            Location = new Point(70, 12),
            Size = new Size(260, 25)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilters();

        var projectLabel = new Label
        {
            Text = "Project:",
            Location = new Point(350, 15),
            AutoSize = true
        };

        _projectFilterCombo = new ComboBox
        {
            Location = new Point(410, 12),
            Size = new Size(220, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _projectFilterCombo.SelectedIndexChanged += ProjectFilterCombo_SelectedIndexChanged;

        // DataGrid
        _dataGrid = new DataGridView
        {
            Location = new Point(10, 45),
            Size = new Size(ClientSize.Width - 20, ClientSize.Height - 95),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D
        };

        // Configure columns
        _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Timestamp",
            HeaderText = "Date & Time",
            FillWeight = 15
        });
        _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Provider",
            HeaderText = "Provider",
            FillWeight = 10
        });
        _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Mode",
            HeaderText = "Mode",
            FillWeight = 10
        });
        _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Project",
            HeaderText = "Project",
            FillWeight = 12
        });
        _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Context",
            HeaderText = "Context",
            FillWeight = 16
        });
        _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Text",
            HeaderText = "Transcription",
            FillWeight = 45
        });

        _dataGrid.DoubleClick += DataGrid_DoubleClick;

        // Buttons
        _copyButton = new Button
        {
            Text = "Copy",
            Location = new Point(10, ClientSize.Height - 40),
            Size = new Size(100, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _copyButton.Click += CopyButton_Click;

        _deleteButton = new Button
        {
            Text = "Delete",
            Location = new Point(120, ClientSize.Height - 40),
            Size = new Size(100, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _deleteButton.Click += DeleteButton_Click;

        _clearAllButton = new Button
        {
            Text = "Clear All",
            Location = new Point(230, ClientSize.Height - 40),
            Size = new Size(100, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            ForeColor = Color.FromArgb(200, 50, 50)
        };
        _clearAllButton.Click += ClearAllButton_Click;

        _tagProjectButton = new Button
        {
            Text = "Tag Project...",
            Location = new Point(340, ClientSize.Height - 40),
            Size = new Size(130, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _tagProjectButton.Click += TagProjectButton_Click;

        // Add controls
        Controls.AddRange(new Control[]
        {
            searchLabel, _searchBox, projectLabel, _projectFilterCombo, _dataGrid,
            _copyButton, _deleteButton, _clearAllButton, _tagProjectButton
        });

        // Keyboard shortcuts
        KeyPreview = true;
        KeyDown += HistoryWindow_KeyDown;
    }

    private void LoadHistory()
    {
        var previousProjectFilter = (_projectFilterCombo.SelectedItem as ProjectFilterItem)?.ProjectId;
        _projects = ProjectStore.LoadProjects()
            .OrderBy(project => project.Archived)
            .ThenBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _allEntries = TranscriptionHistory.LoadHistory()
            .OrderByDescending(e => e.Timestamp)
            .ToList();
        RefreshProjectFilter(previousProjectFilter);
        ApplyFilters();
    }

    private void RefreshGrid()
    {
        _dataGrid.Rows.Clear();

        foreach (var entry in _filteredEntries)
        {
            _dataGrid.Rows.Add(
                entry.FormattedTimestamp,
                entry.Provider,
                entry.Mode,
                entry.ProjectDisplay,
                entry.ContextDisplay,
                entry.Preview
            );
        }

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var hasSelection = _dataGrid.SelectedRows.Count > 0;
        _copyButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        _tagProjectButton.Enabled = hasSelection;
        _clearAllButton.Enabled = _allEntries.Count > 0;
    }

    private void RefreshProjectFilter(string? selectedProjectId)
    {
        _projectFilterCombo.SelectedIndexChanged -= ProjectFilterCombo_SelectedIndexChanged;
        _projectFilterCombo.Items.Clear();
        _projectFilterCombo.Items.Add(new ProjectFilterItem("", "All projects"));
        _projectFilterCombo.Items.Add(new ProjectFilterItem(ProjectFilterItem.UntaggedId, "Untagged"));

        foreach (var project in _projects)
        {
            var label = project.Archived ? $"{project.Name} (archived)" : project.Name;
            _projectFilterCombo.Items.Add(new ProjectFilterItem(project.Id, label));
        }

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedProjectId))
        {
            for (var i = 0; i < _projectFilterCombo.Items.Count; i++)
            {
                if (_projectFilterCombo.Items[i] is ProjectFilterItem item &&
                    string.Equals(item.ProjectId, selectedProjectId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        _projectFilterCombo.SelectedIndex = selectedIndex;
        _projectFilterCombo.SelectedIndexChanged += ProjectFilterCombo_SelectedIndexChanged;
    }

    private void ProjectFilterCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var searchTerm = _searchBox.Text.Trim().ToLowerInvariant();
        var projectFilter = (_projectFilterCombo.SelectedItem as ProjectFilterItem)?.ProjectId ?? "";

        _filteredEntries = _allEntries
            .Where(entry => MatchesProjectFilter(entry, projectFilter))
            .Where(entry => MatchesSearch(entry, searchTerm))
            .ToList();

        RefreshGrid();
    }

    private static bool MatchesProjectFilter(TranscriptionEntry entry, string projectFilter)
    {
        if (string.IsNullOrWhiteSpace(projectFilter))
        {
            return true;
        }

        if (string.Equals(projectFilter, ProjectFilterItem.UntaggedId, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(entry.ProjectId);
        }

        return string.Equals(entry.ProjectId, projectFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSearch(TranscriptionEntry entry, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return true;
        }

        return Contains(entry.Text, searchTerm) ||
               Contains(entry.Provider, searchTerm) ||
               Contains(entry.Mode, searchTerm) ||
               Contains(entry.ProjectDisplay, searchTerm) ||
               Contains(entry.ContextDisplay, searchTerm) ||
               Contains(entry.WindowTitle, searchTerm) ||
               Contains(entry.BrowserUrl, searchTerm);
    }

    private static bool Contains(string? value, string searchTerm) =>
        (value ?? "").ToLowerInvariant().Contains(searchTerm);

    private void CopyButton_Click(object? sender, EventArgs e)
    {
        if (_dataGrid.SelectedRows.Count == 0) return;

        var index = _dataGrid.SelectedRows[0].Index;
        if (index >= 0 && index < _filteredEntries.Count)
        {
            var entry = _filteredEntries[index];
            System.Windows.Forms.Clipboard.SetText(entry.Text);
            MessageBox.Show("Transcription copied to clipboard!", "Copied",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_dataGrid.SelectedRows.Count == 0) return;

        var result = MessageBox.Show(
            "Delete this transcription from history?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (result == DialogResult.Yes)
        {
            var index = _dataGrid.SelectedRows[0].Index;
            if (index >= 0 && index < _filteredEntries.Count)
            {
                var entry = _filteredEntries[index];
                TranscriptionHistory.DeleteEntry(entry);
                LoadHistory();
            }
        }
    }

    private void ClearAllButton_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Delete ALL transcription history? This cannot be undone.",
            "Confirm Clear All",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (result == DialogResult.Yes)
        {
            TranscriptionHistory.ClearHistory();
            LoadHistory();
        }
    }

    private void TagProjectButton_Click(object? sender, EventArgs e)
    {
        if (_dataGrid.SelectedRows.Count == 0) return;

        var index = _dataGrid.SelectedRows[0].Index;
        if (index < 0 || index >= _filteredEntries.Count) return;

        var entry = _filteredEntries[index];
        var currentProject = ProjectStore.GetProject(entry.ProjectId);

        using var dialog = new ProjectPickerDialog(currentProject)
        {
            Text = "Tag History Entry"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        entry.ProjectId = dialog.SelectedProject?.Id ?? "";
        entry.ProjectName = dialog.SelectedProject?.Name ?? "";
        TranscriptionHistory.SaveHistory(_allEntries);
        LoadHistory();
    }

    private void DataGrid_DoubleClick(object? sender, EventArgs e)
    {
        // Double-click copies to clipboard
        CopyButton_Click(sender, e);
    }

    private void HistoryWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C)
        {
            CopyButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            DeleteButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _dataGrid.ClearSelection();
        UpdateButtonStates();
    }

    private sealed class ProjectFilterItem
    {
        public const string UntaggedId = "__untagged";

        public string ProjectId { get; }
        private readonly string _label;

        public ProjectFilterItem(string projectId, string label)
        {
            ProjectId = projectId;
            _label = label;
        }

        public override string ToString() => _label;
    }
}
