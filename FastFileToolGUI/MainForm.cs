using FastFileLib;

namespace FastFileToolGUI;

public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
        PopulateGameVersionComboBox();
    }

    private void PopulateGameVersionComboBox()
    {
        gameVersionComboBox.Items.Clear();
        gameVersionComboBox.Items.AddRange(new object[]
        {
            "COD4 - PS3/Xbox 360 (Unsigned)",
            "COD4 - Xbox 360 (Signed) - Requires original FF",
            "COD4 - PC",
            "COD4 - Wii",
            "WAW - PS3/Xbox 360 (Unsigned)",
            "WAW - Xbox 360 (Signed) - Requires original FF",
            "WAW - Wii",
            "MW2 - PS3/Xbox 360",
            "MW2 - PC"
        });
        gameVersionComboBox.SelectedIndex = 0;
    }

    private (GameVersion gameVersion, string platform, bool xbox360Signed) GetPackSettings()
    {
        return gameVersionComboBox.SelectedIndex switch
        {
            0 => (GameVersion.CoD4, "PS3", false),
            1 => (GameVersion.CoD4, "Xbox360", true),  // Signed
            2 => (GameVersion.CoD4, "PC", false),
            3 => (GameVersion.CoD4, "Wii", false),
            4 => (GameVersion.WaW, "PS3", false),
            5 => (GameVersion.WaW, "Xbox360", true),   // Signed
            6 => (GameVersion.WaW, "Wii", false),
            7 => (GameVersion.MW2, "PS3", false),
            8 => (GameVersion.MW2, "PC", false),
            _ => (GameVersion.CoD4, "PS3", false)
        };
    }

    private void extractBrowseButton_Click(object sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Select FastFile to Extract",
            Filter = "FastFiles (*.ff;*.ffm)|*.ff;*.ffm|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            extractInputTextBox.Text = ofd.FileName;
            extractOutputTextBox.Text = Path.ChangeExtension(ofd.FileName, ".zone");
            UpdateFileInfo(ofd.FileName);
        }
    }

    private void extractOutputBrowseButton_Click(object sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Save Zone File As",
            Filter = "Zone Files (*.zone)|*.zone|All Files (*.*)|*.*",
            FileName = Path.GetFileName(extractOutputTextBox.Text)
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            extractOutputTextBox.Text = sfd.FileName;
        }
    }

    private void extractButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(extractInputTextBox.Text))
        {
            MessageBox.Show("Please select a FastFile to extract.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(extractInputTextBox.Text))
        {
            MessageBox.Show("Input file not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            extractButton.Enabled = false;
            statusLabel.Text = "Extracting...";
            Application.DoEvents();

            Decompress(extractInputTextBox.Text, extractOutputTextBox.Text);

            var fi = new FileInfo(extractOutputTextBox.Text);
            statusLabel.Text = $"Extracted successfully! ({fi.Length:N0} bytes)";
            MessageBox.Show($"Zone file extracted successfully!\n\nOutput: {extractOutputTextBox.Text}\nSize: {fi.Length:N0} bytes",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Extraction failed.";
            MessageBox.Show($"Extraction failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            extractButton.Enabled = true;
        }
    }

    private void packBrowseButton_Click(object sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Select Zone File to Pack",
            Filter = "Zone Files (*.zone)|*.zone|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            packInputTextBox.Text = ofd.FileName;
            packOutputTextBox.Text = Path.ChangeExtension(ofd.FileName, ".ff");
        }
    }

    private void packOutputBrowseButton_Click(object sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Save FastFile As",
            Filter = "FastFiles (*.ff;*.ffm)|*.ff;*.ffm|All Files (*.*)|*.*",
            FileName = Path.GetFileName(packOutputTextBox.Text)
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            packOutputTextBox.Text = sfd.FileName;
        }
    }

    private void packButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(packInputTextBox.Text))
        {
            MessageBox.Show("Please select a Zone file to pack.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(packInputTextBox.Text))
        {
            MessageBox.Show("Input file not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var (gameVersion, platform, xbox360Signed) = GetPackSettings();
        string? originalFfPath = null;

        // For Xbox 360 signed format, prompt for the original FF file
        if (xbox360Signed)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Original Xbox 360 Signed FastFile (to preserve hash table)",
                Filter = "FastFiles (*.ff)|*.ff|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("Xbox 360 signed format requires the original FastFile to preserve the hash table.\n\n" +
                    "Without it, the file won't work on Xbox 360.", "Original FF Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            originalFfPath = ofd.FileName;

            // Verify it's a signed file
            var info = FastFileInfo.FromFile(originalFfPath);
            if (!info.IsSigned)
            {
                MessageBox.Show("The selected file is not a signed Xbox 360 FastFile.\n\n" +
                    "Please select a signed file (IWff0100 header) or use the unsigned format.",
                    "Not Signed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        try
        {
            packButton.Enabled = false;
            statusLabel.Text = "Packing...";
            Application.DoEvents();

            if (xbox360Signed && originalFfPath != null)
            {
                FastFileProcessor.CompressXbox360Signed(packInputTextBox.Text, packOutputTextBox.Text, gameVersion, originalFfPath);
            }
            else
            {
                Compress(packInputTextBox.Text, packOutputTextBox.Text, gameVersion, platform);
            }

            var fi = new FileInfo(packOutputTextBox.Text);
            statusLabel.Text = $"Packed successfully! ({fi.Length:N0} bytes)";

            string formatNote = xbox360Signed ? " (Xbox 360 Signed Format)" : "";
            MessageBox.Show($"FastFile created successfully!{formatNote}\n\nOutput: {packOutputTextBox.Text}\nSize: {fi.Length:N0} bytes",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Packing failed.";
            MessageBox.Show($"Packing failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            packButton.Enabled = true;
        }
    }

    private void UpdateFileInfo(string filePath)
    {
        try
        {
            // Use the library's FastFileInfo for detection
            var ffInfo = FastFileInfo.FromFile(filePath);
            var fileInfo = new FileInfo(filePath);

            string signedStr = ffInfo.IsSigned ? "Signed" : "Unsigned";

            fileInfoLabel.Text = $"Header: {ffInfo.Magic} | {signedStr} | Studio: {ffInfo.Studio} | Game: {ffInfo.GameName} | Platform: {ffInfo.Platform} | Version: 0x{ffInfo.Version:X}";
            fileInfoLabel.ForeColor = Color.DarkGreen;

            // Update detailed info
            UpdateDetailedInfo(fileInfo.Length, ffInfo.Magic, ffInfo.IsSigned, ffInfo.Studio, ffInfo.GameName, ffInfo.Platform, ffInfo.Version);
        }
        catch (Exception ex)
        {
            fileInfoLabel.Text = $"Error reading file: {ex.Message}";
            fileInfoLabel.ForeColor = Color.Red;
        }
    }

    private void UpdateDetailedInfo(long fileSize, string header, bool isSigned, string studio, string game, string platform, uint version)
    {
        detailsTextBox.Clear();
        detailsTextBox.AppendText($"File Size: {fileSize:N0} bytes ({fileSize / 1024.0 / 1024.0:F2} MB)\r\n");
        detailsTextBox.AppendText($"Header Magic: {header}\r\n");
        detailsTextBox.AppendText($"Signed: {(isSigned ? "Yes (RSA2048)" : "No")}\r\n");
        detailsTextBox.AppendText($"Studio: {studio}\r\n");
        detailsTextBox.AppendText($"Game: {game}\r\n");
        detailsTextBox.AppendText($"Platform: {platform}\r\n");
        detailsTextBox.AppendText($"Version: 0x{version:X} ({version})\r\n");
        detailsTextBox.AppendText($"\r\n");

        // Check for unsupported games (detection only, extraction/packing won't work)
        bool isUnsupportedGame = game == "BO1" || game == "MW3" || game == "BO2" || game == "Quantum of Solace";

        if (isUnsupportedGame)
        {
            detailsTextBox.AppendText("Warning: This game is not fully supported.\r\n");
            detailsTextBox.AppendText("Detection only - extraction/packing may not work.\r\n");
            detailsTextBox.AppendText("Supported games: CoD4, WaW, MW2\r\n");
        }
        else
        {
            detailsTextBox.AppendText("This FastFile can be modified.\r\n");
            if (isSigned)
            {
                detailsTextBox.AppendText("Note: Xbox 360 requires a patched XEX to load modified FastFiles.\r\n");
            }
        }
    }

    private void Decompress(string inputPath, string outputPath)
    {
        int blockCount = FastFileProcessor.Decompress(inputPath, outputPath);
        statusLabel.Text = $"Extracted {blockCount} blocks";
    }

    private void Compress(string inputPath, string outputPath, GameVersion gameVersion, string platform)
    {
        int blockCount = FastFileProcessor.Compress(inputPath, outputPath, gameVersion, platform);
        statusLabel.Text = $"Packed {blockCount} blocks";
    }

    private void MainForm_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void MainForm_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string file = files[0];
            string ext = Path.GetExtension(file).ToLower();

            if (ext == ".ff")
            {
                tabControl.SelectedIndex = 0; // Extract tab
                extractInputTextBox.Text = file;
                extractOutputTextBox.Text = Path.ChangeExtension(file, ".zone");
                UpdateFileInfo(file);
            }
            else if (ext == ".zone")
            {
                tabControl.SelectedIndex = 1; // Pack tab
                packInputTextBox.Text = file;
                packOutputTextBox.Text = Path.ChangeExtension(file, ".ff");
            }
        }
    }
}
