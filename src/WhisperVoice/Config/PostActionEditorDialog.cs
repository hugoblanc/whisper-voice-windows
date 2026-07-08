using System.Diagnostics;

namespace WhisperVoice.Config;

public class PostActionEditorDialog : Form
{
    private readonly HashSet<string> _existingIds;
    private readonly TextBox _nameTextBox;
    private readonly TextBox _commandTextBox;
    private readonly Label _testStatusLabel;
    private readonly Button _testButton;

    public PostActionConfig Action { get; private set; }

    public PostActionEditorDialog(PostActionConfig? action, IEnumerable<string> existingIds)
    {
        Action = action?.Clone() ?? new PostActionConfig
        {
            Type = "command",
            Label = "",
            Command = ""
        };
        _existingIds = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);

        Text = action == null ? "Add Post Action" : "Edit Post Action";
        ClientSize = new Size(620, 420);
        MinimumSize = new Size(520, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);

        var nameLabel = new Label
        {
            Text = "Name:",
            Location = new Point(16, 18),
            AutoSize = true
        };

        _nameTextBox = new TextBox
        {
            Location = new Point(16, 46),
            Size = new Size(ClientSize.Width - 32, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = Action.Label
        };

        var commandLabel = new Label
        {
            Text = "Shell command (cmd.exe /C):",
            Location = new Point(16, 94),
            AutoSize = true
        };

        _commandTextBox = new TextBox
        {
            Location = new Point(16, 122),
            Size = new Size(ClientSize.Width - 32, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            Text = Action.Command
        };

        var variablesTextBox = new TextBox
        {
            Location = new Point(16, 286),
            Size = new Size(ClientSize.Width - 32, 58),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Text = "Variables: %WV_TRANSCRIPTION%, %WV_RAW_TRANSCRIPTION%, %WV_APP_PROCESS%, %WV_APP_WINDOW_TITLE%, %WV_BROWSER_URL%, %WV_BROWSER_HOST%, %WV_WORKSPACE%, %WV_PROJECT%, %WV_MODE%, %WV_PROVIDER%"
        };

        _testButton = new Button
        {
            Text = "Test",
            Location = new Point(16, 360),
            Size = new Size(96, 34),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _testButton.Click += TestButton_Click;

        _testStatusLabel = new Label
        {
            Text = "",
            Location = new Point(122, 366),
            Size = new Size(250, 24),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(ClientSize.Width - 214, 360),
            Size = new Size(94, 34),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        saveButton.Click += SaveButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(ClientSize.Width - 110, 360),
            Size = new Size(94, 34),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        Controls.AddRange(new Control[]
        {
            nameLabel,
            _nameTextBox,
            commandLabel,
            _commandTextBox,
            variablesTextBox,
            _testButton,
            _testStatusLabel,
            saveButton,
            cancelButton
        });

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var name = _nameTextBox.Text.Trim();
        var command = _commandTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter an action name.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            MessageBox.Show("Please enter a command.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var id = Action.Id;
        if (string.IsNullOrWhiteSpace(id) || _existingIds.Contains(id))
        {
            id = PostActionConfig.CreateUniqueId(name, _existingIds);
        }

        Action = new PostActionConfig
        {
            Id = id,
            Label = name,
            Type = "command",
            Command = command
        };
    }

    private async void TestButton_Click(object? sender, EventArgs e)
    {
        var command = _commandTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(command)) return;

        _testButton.Enabled = false;
        _testStatusLabel.Text = "Testing...";
        _testStatusLabel.ForeColor = Color.Gray;

        try
        {
            var result = await Task.Run(() => RunTestCommand(command));
            _testStatusLabel.Text = result.Success ? "OK (exit 0)" : result.Message;
            _testStatusLabel.ForeColor = result.Success ? Color.Green : Color.Red;
        }
        finally
        {
            _testButton.Enabled = true;
        }
    }

    private static (bool Success, string Message) RunTestCommand(string command)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + command,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false
            };

            process.StartInfo.Environment["WV_TRANSCRIPTION"] = "Test transcription from Whisper Voice";
            process.StartInfo.Environment["WV_RAW_TRANSCRIPTION"] = "Test transcription from Whisper Voice";
            process.StartInfo.Environment["WV_APP_PROCESS"] = "TestApp";
            process.StartInfo.Environment["WV_APP_NAME"] = "TestApp";
            process.StartInfo.Environment["WV_APP_PATH"] = "";
            process.StartInfo.Environment["WV_APP_WINDOW_TITLE"] = "Test Window";
            process.StartInfo.Environment["WV_BROWSER_URL"] = "https://example.com/test";
            process.StartInfo.Environment["WV_BROWSER_HOST"] = "example.com";
            process.StartInfo.Environment["WV_WORKSPACE"] = "test-workspace";
            process.StartInfo.Environment["WV_PROJECT_ID"] = "test-project-id";
            process.StartInfo.Environment["WV_PROJECT"] = "test-project";
            process.StartInfo.Environment["WV_MODE"] = "Brut";
            process.StartInfo.Environment["WV_PROVIDER"] = "openai";

            process.Start();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                return (false, "Timeout");
            }

            return process.ExitCode == 0
                ? (true, "OK")
                : (false, $"Exit {process.ExitCode}: {Truncate(stderr.Trim(), 120)}");
        }
        catch (Exception ex)
        {
            return (false, Truncate(ex.Message, 120));
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
