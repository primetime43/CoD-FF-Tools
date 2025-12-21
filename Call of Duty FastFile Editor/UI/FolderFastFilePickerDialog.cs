using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastFileLib;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Dialog that displays FastFiles found in a folder and allows the user to select one to open.
    /// </summary>
    public class FolderFastFilePickerDialog : Form
    {
        private ListView _fileListView;
        private Button _openButton;
        private Button _cancelButton;
        private Button _browseButton;
        private TextBox _folderPathTextBox;
        private CheckBox _includeSubfoldersCheckBox;
        private Label _statusLabel;

        /// <summary>
        /// Gets the selected file path.
        /// </summary>
        public string SelectedFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last used folder path.
        /// </summary>
        public static string LastFolderPath { get; set; } = string.Empty;

        public FolderFastFilePickerDialog()
        {
            InitializeComponents();

            // Load last used folder if available
            if (!string.IsNullOrEmpty(LastFolderPath) && Directory.Exists(LastFolderPath))
            {
                _folderPathTextBox.Text = LastFolderPath;
                ScanFolder(LastFolderPath, _includeSubfoldersCheckBox.Checked);
            }
        }

        private void InitializeComponents()
        {
            this.Text = "Open FastFile from Folder";
            this.Size = new Size(750, 500);
            this.MinimumSize = new Size(550, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            // Enable drag-drop for the form
            this.AllowDrop = true;
            this.DragEnter += Form_DragEnter;
            this.DragDrop += Form_DragDrop;

            // Top panel with folder selection using TableLayoutPanel for reliable layout
            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(5)
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // TextBox
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Button
            topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var folderLabel = new Label
            {
                Text = "Folder:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 8, 3, 3)
            };

            _folderPathTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 5, 3, 3)
            };
            _folderPathTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (Directory.Exists(_folderPathTextBox.Text))
                        ScanFolder(_folderPathTextBox.Text, _includeSubfoldersCheckBox.Checked);
                }
            };

            _browseButton = new Button
            {
                Text = "Browse...",
                Width = 85,
                Height = 27,
                Margin = new Padding(3, 3, 3, 3)
            };
            _browseButton.Click += BrowseButton_Click;

            _includeSubfoldersCheckBox = new CheckBox
            {
                Text = "Include subfolders",
                AutoSize = true,
                Margin = new Padding(3, 3, 3, 3)
            };
            _includeSubfoldersCheckBox.CheckedChanged += (s, e) =>
            {
                if (Directory.Exists(_folderPathTextBox.Text))
                    ScanFolder(_folderPathTextBox.Text, _includeSubfoldersCheckBox.Checked);
            };

            var dragDropLabel = new Label
            {
                Text = "(You can also drag and drop a folder here)",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(this.Font, FontStyle.Italic),
                Margin = new Padding(20, 5, 3, 3)
            };

            topPanel.Controls.Add(folderLabel, 0, 0);
            topPanel.Controls.Add(_folderPathTextBox, 1, 0);
            topPanel.Controls.Add(_browseButton, 2, 0);
            topPanel.Controls.Add(_includeSubfoldersCheckBox, 1, 1);
            topPanel.Controls.Add(dragDropLabel, 2, 1);

            // File list view
            _fileListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _fileListView.Columns.Add("File Name", 220);
            _fileListView.Columns.Add("Size", 80);
            _fileListView.Columns.Add("Game", 55);
            _fileListView.Columns.Add("Platform", 65);
            _fileListView.Columns.Add("Path", 200);
            _fileListView.DoubleClick += FileListView_DoubleClick;
            _fileListView.SelectedIndexChanged += FileListView_SelectedIndexChanged;

            // Bottom panel with buttons
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45
            };

            _statusLabel = new Label
            {
                Location = new Point(10, 14),
                AutoSize = true,
                Text = "Select a folder to scan for FastFiles"
            };

            _openButton = new Button
            {
                Text = "Open",
                DialogResult = DialogResult.OK,
                Location = new Point(this.ClientSize.Width - 180, 10),
                Width = 80,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false
            };
            _openButton.Click += OpenButton_Click;

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(this.ClientSize.Width - 90, 10),
                Width = 80,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            bottomPanel.Controls.AddRange(new Control[] { _statusLabel, _openButton, _cancelButton });

            this.Controls.Add(_fileListView);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);

            this.AcceptButton = _openButton;
            this.CancelButton = _cancelButton;
        }

        private void Form_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths?.Length == 1 && Directory.Exists(paths[0]))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void Form_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            {
                string path = paths[0];
                if (Directory.Exists(path))
                {
                    _folderPathTextBox.Text = path;
                    ScanFolder(path, _includeSubfoldersCheckBox.Checked);
                }
            }
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select a folder containing FastFiles (.ff)",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true
            };

            if (!string.IsNullOrEmpty(_folderPathTextBox.Text) && Directory.Exists(_folderPathTextBox.Text))
            {
                folderDialog.InitialDirectory = _folderPathTextBox.Text;
            }

            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                _folderPathTextBox.Text = folderDialog.SelectedPath;
                ScanFolder(folderDialog.SelectedPath, _includeSubfoldersCheckBox.Checked);
            }
        }

        private void ScanFolder(string folderPath, bool includeSubfolders)
        {
            _fileListView.Items.Clear();
            _openButton.Enabled = false;

            try
            {
                var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(folderPath, "*.ff", searchOption);

                var fileInfos = new List<(FileInfo info, string game, string platform)>();

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var (game, platform) = DetectGameAndPlatform(file);
                    fileInfos.Add((fileInfo, game, platform));
                }

                // Sort by name
                fileInfos = fileInfos.OrderBy(f => f.info.Name).ToList();

                foreach (var (info, game, platform) in fileInfos)
                {
                    var item = new ListViewItem(info.Name);
                    item.SubItems.Add(FastFileInfo.FormatFileSize(info.Length));
                    item.SubItems.Add(game);
                    item.SubItems.Add(platform);
                    item.SubItems.Add(GetRelativePath(folderPath, info.DirectoryName ?? ""));
                    item.Tag = info.FullName;

                    // Color code by game
                    item.ForeColor = game switch
                    {
                        "WaW" => Color.DarkGreen,
                        "CoD4" => Color.DarkBlue,
                        "MW2" => Color.DarkRed,
                        _ => Color.Gray
                    };

                    _fileListView.Items.Add(item);
                }

                _statusLabel.Text = $"Found {files.Length} FastFile(s)";
                LastFolderPath = folderPath;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
        }

        private (string game, string platform) DetectGameAndPlatform(string filePath)
        {
            try
            {
                var info = FastFileInfo.FromFile(filePath);
                return (info.GameName, info.Platform);
            }
            catch
            {
                return ("?", "?");
            }
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || fullPath.Equals(basePath, StringComparison.OrdinalIgnoreCase))
                return ".";

            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                var relative = fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
                return string.IsNullOrEmpty(relative) ? "." : relative;
            }

            return fullPath;
        }

        private void FileListView_DoubleClick(object? sender, EventArgs e)
        {
            if (_fileListView.SelectedItems.Count > 0)
            {
                OpenButton_Click(sender, e);
            }
        }

        private void FileListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _openButton.Enabled = _fileListView.SelectedItems.Count > 0;
        }

        private void OpenButton_Click(object? sender, EventArgs e)
        {
            if (_fileListView.SelectedItems.Count > 0)
            {
                SelectedFilePath = _fileListView.SelectedItems[0].Tag as string ?? string.Empty;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}
