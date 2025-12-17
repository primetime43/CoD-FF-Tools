using FastFileLib;

namespace FastFileConverterGUI;

public partial class Form1 : Form
{
    private TextBox _inputPathTextBox = null!;
    private TextBox _outputPathTextBox = null!;
    private ComboBox _targetPlatformCombo = null!;
    private Button _browseInputButton = null!;
    private Button _browseOutputButton = null!;
    private Button _convertButton = null!;
    private Button _analyzeButton = null!;
    private RichTextBox _logTextBox = null!;
    private ProgressBar _progressBar = null!;
    private GroupBox _sourceInfoGroup = null!;
    private Label _sourceGameLabel = null!;
    private Label _sourcePlatformLabel = null!;
    private Label _sourceSignedLabel = null!;
    private Label _sourceSizeLabel = null!;

    private FastFileAnalysis? _currentAnalysis;

    public Form1()
    {
        InitializeComponent();
        SetupUI();
    }

    private void SetupUI()
    {
        this.Text = "FastFile Platform Converter";
        this.Size = new Size(700, 600);
        this.MinimumSize = new Size(600, 500);
        this.StartPosition = FormStartPosition.CenterScreen;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 5,
            ColumnCount = 1
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Input
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Source info
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Output
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Log

        // Input section
        var inputGroup = new GroupBox
        {
            Text = "Input FastFile",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10)
        };

        var inputPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true
        };
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _inputPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        _browseInputButton = new Button { Text = "Browse...", Width = 80 };
        _analyzeButton = new Button { Text = "Analyze", Width = 80, Enabled = false };

        _browseInputButton.Click += BrowseInputButton_Click;
        _analyzeButton.Click += AnalyzeButton_Click;

        inputPanel.Controls.Add(_inputPathTextBox, 0, 0);
        inputPanel.Controls.Add(_browseInputButton, 1, 0);
        inputPanel.Controls.Add(_analyzeButton, 2, 0);
        inputGroup.Controls.Add(inputPanel);

        // Source info section
        _sourceInfoGroup = new GroupBox
        {
            Text = "Source File Information",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10),
            Visible = false
        };

        var infoPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            AutoSize = true
        };
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _sourceGameLabel = new Label { Text = "Game: -", AutoSize = true };
        _sourcePlatformLabel = new Label { Text = "Platform: -", AutoSize = true };
        _sourceSignedLabel = new Label { Text = "Signed: -", AutoSize = true };
        _sourceSizeLabel = new Label { Text = "Size: -", AutoSize = true };

        infoPanel.Controls.Add(_sourceGameLabel, 0, 0);
        infoPanel.Controls.Add(_sourcePlatformLabel, 1, 0);
        infoPanel.Controls.Add(_sourceSignedLabel, 2, 0);
        infoPanel.Controls.Add(_sourceSizeLabel, 3, 0);
        _sourceInfoGroup.Controls.Add(infoPanel);

        // Output section
        var outputGroup = new GroupBox
        {
            Text = "Output",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10)
        };

        var outputPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            AutoSize = true
        };
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _outputPathTextBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        _browseOutputButton = new Button { Text = "Browse...", Width = 80 };

        var platformLabel = new Label { Text = "Target:", AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
        _targetPlatformCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 100
        };
        _targetPlatformCombo.Items.AddRange(new object[] { "PS3", "Xbox 360", "PC" });
        _targetPlatformCombo.SelectedIndex = 0;
        _targetPlatformCombo.SelectedIndexChanged += TargetPlatformCombo_SelectedIndexChanged;

        _browseOutputButton.Click += BrowseOutputButton_Click;

        outputPanel.Controls.Add(_outputPathTextBox, 0, 0);
        outputPanel.Controls.Add(_browseOutputButton, 1, 0);
        outputPanel.Controls.Add(platformLabel, 2, 0);
        outputPanel.Controls.Add(_targetPlatformCombo, 3, 0);
        outputGroup.Controls.Add(outputPanel);

        // Convert button and progress
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        _convertButton = new Button
        {
            Text = "Convert",
            Width = 120,
            Height = 35,
            Enabled = false
        };
        _convertButton.Click += ConvertButton_Click;

        _progressBar = new ProgressBar
        {
            Width = 200,
            Height = 25,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Visible = false
        };

        buttonPanel.Controls.Add(_convertButton);
        buttonPanel.Controls.Add(_progressBar);

        // Log section
        var logGroup = new GroupBox
        {
            Text = "Log",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _logTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Font = new Font("Consolas", 9)
        };
        logGroup.Controls.Add(_logTextBox);

        // Add all to main panel
        mainPanel.Controls.Add(inputGroup, 0, 0);
        mainPanel.Controls.Add(_sourceInfoGroup, 0, 1);
        mainPanel.Controls.Add(outputGroup, 0, 2);
        mainPanel.Controls.Add(buttonPanel, 0, 3);
        mainPanel.Controls.Add(logGroup, 0, 4);

        this.Controls.Add(mainPanel);

        // Enable drag and drop
        this.AllowDrop = true;
        this.DragEnter += Form1_DragEnter;
        this.DragDrop += Form1_DragDrop;

        Log("FastFile Platform Converter ready.");
        Log("Supports: CoD4, WaW, MW2 (PS3, Xbox 360, PC)");
        Log("");
        Log("How it works:");
        Log("  - Select a mod FF to convert (e.g., Xbox 360 patch_mp.ff)");
        Log("  - Extracts raw files (.gsc, .cfg, etc.) from the mod");
        Log("  - Builds a fresh PS3-compatible zone with correct headers");
        Log("  - Compresses to PS3 FastFile format");
        Log("");
        Log("Drag and drop a .ff file or click Browse to begin.");
    }

    private void UpdateConvertButtonState()
    {
        bool hasInput = !string.IsNullOrEmpty(_inputPathTextBox.Text);
        bool hasOutput = !string.IsNullOrEmpty(_outputPathTextBox.Text);
        bool hasAnalysis = _currentAnalysis?.IsValid == true;

        _convertButton.Enabled = hasInput && hasOutput && hasAnalysis;
    }

    private void Form1_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length == 1 && files[0].EndsWith(".ff", StringComparison.OrdinalIgnoreCase))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
        }
        e.Effect = DragDropEffects.None;
    }

    private void Form1_DragDrop(object? sender, DragEventArgs e)
    {
        var files = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
        if (files?.Length == 1)
        {
            LoadFile(files[0]);
        }
    }

    private void BrowseInputButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select FastFile",
            Filter = "FastFiles (*.ff)|*.ff|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadFile(dialog.FileName);
        }
    }

    private void LoadFile(string path)
    {
        _inputPathTextBox.Text = path;
        _analyzeButton.Enabled = true;

        // Set default output path before analysis so UpdateConvertButtonState has it
        string dir = Path.GetDirectoryName(path) ?? "";
        string name = Path.GetFileNameWithoutExtension(path);
        string targetPlatform = _targetPlatformCombo.SelectedItem?.ToString()?.Replace(" ", "") ?? "PS3";
        _outputPathTextBox.Text = Path.Combine(dir, $"{name}_converted_{targetPlatform}.ff");

        // Auto-analyze (calls UpdateConvertButtonState which needs output path)
        AnalyzeFile(path);
    }

    private void AnalyzeButton_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_inputPathTextBox.Text))
        {
            AnalyzeFile(_inputPathTextBox.Text);
        }
    }

    private void AnalyzeFile(string path)
    {
        Log("");
        Log($"Analyzing: {Path.GetFileName(path)}");

        try
        {
            _currentAnalysis = FastFileConverter.Analyze(path);

            if (_currentAnalysis.IsValid)
            {
                _sourceGameLabel.Text = $"Game: {_currentAnalysis.GameName}";
                _sourcePlatformLabel.Text = $"Platform: {_currentAnalysis.DetectedPlatform}";
                _sourceSignedLabel.Text = $"Signed: {(_currentAnalysis.IsSigned ? "Yes" : "No")}";
                _sourceSizeLabel.Text = $"Size: {FormatSize(_currentAnalysis.FileSize)}";
                _sourceInfoGroup.Visible = true;

                Log($"  Game: {_currentAnalysis.GameName}");
                Log($"  Platform: {_currentAnalysis.DetectedPlatform}");
                Log($"  Signed: {(_currentAnalysis.IsSigned ? "Yes (Xbox 360 MP)" : "No")}");
                Log($"  Size: {FormatSize(_currentAnalysis.FileSize)}");

                foreach (var note in _currentAnalysis.Notes)
                {
                    LogWarning($"  Note: {note}");
                }

                UpdateConvertButtonState();
                LogSuccess("  File is valid and can be converted.");
            }
            else
            {
                _sourceInfoGroup.Visible = false;
                _convertButton.Enabled = false;

                LogError("  File analysis failed:");
                foreach (var note in _currentAnalysis.Notes)
                {
                    LogError($"    {note}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"  Error analyzing file: {ex.Message}");
            _convertButton.Enabled = false;
        }
    }

    private void TargetPlatformCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Update output filename when target platform changes
        if (!string.IsNullOrEmpty(_inputPathTextBox.Text))
        {
            string dir = Path.GetDirectoryName(_inputPathTextBox.Text) ?? "";
            string name = Path.GetFileNameWithoutExtension(_inputPathTextBox.Text);
            string targetPlatform = _targetPlatformCombo.SelectedItem?.ToString()?.Replace(" ", "") ?? "PS3";
            _outputPathTextBox.Text = Path.Combine(dir, $"{name}_converted_{targetPlatform}.ff");
        }
    }

    private void BrowseOutputButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save Converted FastFile",
            Filter = "FastFiles (*.ff)|*.ff|All Files (*.*)|*.*",
            FilterIndex = 1,
            FileName = Path.GetFileName(_outputPathTextBox.Text)
        };

        if (!string.IsNullOrEmpty(_outputPathTextBox.Text))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(_outputPathTextBox.Text);
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _outputPathTextBox.Text = dialog.FileName;
        }
    }

    private async void ConvertButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_inputPathTextBox.Text) ||
            string.IsNullOrEmpty(_outputPathTextBox.Text))
        {
            MessageBox.Show("Please select input and output files.", "Missing Information",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_inputPathTextBox.Text.Equals(_outputPathTextBox.Text, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Input and output files cannot be the same.", "Invalid Output",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string targetPlatformStr = _targetPlatformCombo.SelectedItem?.ToString() ?? "PS3";
        Platform targetPlatform = targetPlatformStr switch
        {
            "Xbox 360" => Platform.Xbox360,
            "PC" => Platform.PC,
            _ => Platform.PS3
        };

        SetUIEnabled(false);
        _progressBar.Visible = true;

        Log("");
        Log($"Converting to {targetPlatformStr}...");
        Log($"  Source: {Path.GetFileName(_inputPathTextBox.Text)}");

        try
        {
            ConversionResult result;

            // Use the ZoneBuilder approach for PS3 conversions (builds fresh zone from raw files)
            if (targetPlatform == Platform.PS3)
            {
                result = await Task.Run(() =>
                    FastFileConverter.ConvertUsingBaseZone(_inputPathTextBox.Text, "", _outputPathTextBox.Text));
            }
            else
            {
                // Direct conversion mode for other platforms
                result = await Task.Run(() =>
                    FastFileConverter.Convert(_inputPathTextBox.Text, _outputPathTextBox.Text, targetPlatform));
            }

            if (result.Success)
            {
                LogSuccess($"Conversion successful!");
                Log($"  Source: {result.SourcePlatform}");
                Log($"  Target: {result.TargetPlatform}");
                Log($"  Game: {result.GameVersion}");
                Log($"  Blocks processed: {result.BlocksProcessed}");
                Log($"  Original size: {FormatSize(result.OriginalSize)}");
                Log($"  Converted size: {FormatSize(result.ConvertedSize)}");

                if (result.ReplacedFiles.Count > 0)
                {
                    LogSuccess($"  Included {result.ReplacedFiles.Count} raw files in new zone");
                }

                foreach (var warning in result.Warnings)
                {
                    Log($"  {warning}");
                }

                Log($"  Output: {_outputPathTextBox.Text}");

                MessageBox.Show(
                    $"Conversion successful!\n\n" +
                    $"Source: {result.SourcePlatform}\n" +
                    $"Target: {result.TargetPlatform}\n" +
                    $"Raw files: {result.ReplacedFiles.Count}\n" +
                    $"Size: {FormatSize(result.OriginalSize)} -> {FormatSize(result.ConvertedSize)}",
                    "Conversion Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                LogError($"Conversion failed: {result.Message}");
                MessageBox.Show(result.Message, "Conversion Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            LogError($"Error: {ex.Message}");
            MessageBox.Show($"Conversion error: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Visible = false;
            SetUIEnabled(true);
        }
    }

    private void SetUIEnabled(bool enabled)
    {
        _browseInputButton.Enabled = enabled;
        _browseOutputButton.Enabled = enabled;
        _convertButton.Enabled = enabled;
        _analyzeButton.Enabled = enabled;
        _targetPlatformCombo.Enabled = enabled;
    }

    private void Log(string message)
    {
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.SelectionColor = Color.White;
        _logTextBox.AppendText(message + Environment.NewLine);
        _logTextBox.ScrollToCaret();
    }

    private void LogSuccess(string message)
    {
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.SelectionColor = Color.LightGreen;
        _logTextBox.AppendText(message + Environment.NewLine);
        _logTextBox.ScrollToCaret();
    }

    private void LogWarning(string message)
    {
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.SelectionColor = Color.Yellow;
        _logTextBox.AppendText(message + Environment.NewLine);
        _logTextBox.ScrollToCaret();
    }

    private void LogError(string message)
    {
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.SelectionColor = Color.Salmon;
        _logTextBox.AppendText(message + Environment.NewLine);
        _logTextBox.ScrollToCaret();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
