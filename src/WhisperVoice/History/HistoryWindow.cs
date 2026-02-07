using WhisperVoice.Logging;

namespace WhisperVoice.History;

/// <summary>
/// Window to view, search, and manage transcription history
/// </summary>
public class HistoryWindow : Form
{
    private DataGridView _dataGrid;
    private TextBox _searchBox;
    private Button _copyButton;
    private Button _deleteButton;
    private Button _clearAllButton;
    private List<TranscriptionEntry> _allEntries;
    private List<TranscriptionEntry> _filteredEntries;

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
            Size = new Size(300, 25)
        };
        _searchBox.TextChanged += SearchBox_TextChanged;

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
            Name = "Text",
            HeaderText = "Transcription",
            FillWeight = 65
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

        // Add controls
        Controls.AddRange(new Control[]
        {
            searchLabel, _searchBox, _dataGrid,
            _copyButton, _deleteButton, _clearAllButton
        });

        // Keyboard shortcuts
        KeyPreview = true;
        KeyDown += HistoryWindow_KeyDown;
    }

    private void LoadHistory()
    {
        _allEntries = TranscriptionHistory.LoadHistory()
            .OrderByDescending(e => e.Timestamp)
            .ToList();
        _filteredEntries = _allEntries;
        RefreshGrid();
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
        _clearAllButton.Enabled = _allEntries.Count > 0;
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        var searchTerm = _searchBox.Text.ToLower();

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _filteredEntries = _allEntries;
        }
        else
        {
            _filteredEntries = _allEntries
                .Where(e => e.Text.ToLower().Contains(searchTerm) ||
                           e.Provider.ToLower().Contains(searchTerm) ||
                           e.Mode.ToLower().Contains(searchTerm))
                .ToList();
        }

        RefreshGrid();
    }

    private void CopyButton_Click(object? sender, EventArgs e)
    {
        if (_dataGrid.SelectedRows.Count == 0) return;

        var index = _dataGrid.SelectedRows[0].Index;
        if (index >= 0 && index < _filteredEntries.Count)
        {
            var entry = _filteredEntries[index];
            Clipboard.SetText(entry.Text);
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
}
