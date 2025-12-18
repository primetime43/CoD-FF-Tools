using System.Text;
using System.IO.Compression;
using FastFileLib;

namespace FastFileToolGUI;

public partial class MainForm : Form
{
    // Version detection mapping (version int -> game/platform info)
    private static readonly Dictionary<uint, (string Game, string[] Platforms)> VersionMap = new()
    {
        // COD4
        { 0x01, ("COD4", new[] { "PS3", "Xbox 360" }) },
        { 0x05, ("COD4", new[] { "PC" }) },
        { 0x1A2, ("COD4", new[] { "Wii" }) },
        // WAW
        { 0x183, ("WAW", new[] { "PS3", "Xbox 360", "PC" }) },
        { 0x19B, ("WAW", new[] { "Wii" }) },
        // MW2
        { 0x10D, ("MW2", new[] { "PS3", "Xbox 360" }) },
        { 0x114, ("MW2", new[] { "PC" }) },
        // BO1
        { 0x1D9, ("BO1", new[] { "PS3", "Xbox 360", "PC" }) },
        { 0x1DD, ("BO1", new[] { "Wii" }) },
        // MW3
        { 0x70, ("MW3", new[] { "PS3", "Xbox 360" }) },
        { 0x6B, ("MW3", new[] { "Wii" }) },
        // Note: MW3 PC uses 0x01 which conflicts with COD4
        // BO2
        { 0x92, ("BO2", new[] { "PS3", "Xbox 360" }) },
        { 0x93, ("BO2", new[] { "PC" }) },
        { 0x94, ("BO2", new[] { "Wii U" }) },
        // Quantum of Solace
        { 0x1D6, ("Quantum of Solace", new[] { "PS3", "Xbox 360", "PC" }) },
        { 0x1D2, ("Quantum of Solace", new[] { "Wii" }) },
    };

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
            "COD4 - PC",
            "COD4 - Wii",
            "WAW - PS3/Xbox 360/PC",
            "WAW - Wii",
            "MW2 - PS3/Xbox 360",
            "MW2 - PC"
        });
        gameVersionComboBox.SelectedIndex = 0;
    }

    private (GameVersion gameVersion, string platform) GetPackSettings()
    {
        return gameVersionComboBox.SelectedIndex switch
        {
            0 => (GameVersion.CoD4, "PS3"),
            1 => (GameVersion.CoD4, "PC"),
            2 => (GameVersion.CoD4, "Wii"),
            3 => (GameVersion.WaW, "PS3"),
            4 => (GameVersion.WaW, "Wii"),
            5 => (GameVersion.MW2, "PS3"),
            6 => (GameVersion.MW2, "PC"),
            _ => (GameVersion.CoD4, "PS3")
        };
    }

    private void extractBrowseButton_Click(object sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Select FastFile to Extract",
            Filter = "FastFiles (*.ff)|*.ff|All Files (*.*)|*.*"
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
            Filter = "FastFiles (*.ff)|*.ff|All Files (*.*)|*.*",
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

        try
        {
            packButton.Enabled = false;
            statusLabel.Text = "Packing...";
            Application.DoEvents();

            var (gameVersion, platform) = GetPackSettings();
            Compress(packInputTextBox.Text, packOutputTextBox.Text, gameVersion, platform);

            var fi = new FileInfo(packOutputTextBox.Text);
            statusLabel.Text = $"Packed successfully! ({fi.Length:N0} bytes)";
            MessageBox.Show($"FastFile created successfully!\n\nOutput: {packOutputTextBox.Text}\nSize: {fi.Length:N0} bytes",
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
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            byte[] header = br.ReadBytes(8);
            byte[] versionBytes = br.ReadBytes(4);

            string headerStr = Encoding.ASCII.GetString(header);

            // Determine header type
            string headerType;
            bool isSigned;
            string studio = "IW";

            if (headerStr == "IWffu100")
            {
                headerType = "IWffu100";
                isSigned = false;
            }
            else if (headerStr == "IWff0100")
            {
                headerType = "IWff0100";
                isSigned = true;
            }
            else if (headerStr == "TAff0100")
            {
                headerType = "TAff0100";
                isSigned = true;
                studio = "Treyarch";
            }
            else if (headerStr == "S1ff0100")
            {
                headerType = "S1ff0100";
                isSigned = true;
                studio = "Sledgehammer";
            }
            else
            {
                fileInfoLabel.Text = $"Unknown header: {headerStr}";
                fileInfoLabel.ForeColor = Color.Red;
                return;
            }

            // Parse version (big-endian)
            uint version = (uint)((versionBytes[0] << 24) | (versionBytes[1] << 16) | (versionBytes[2] << 8) | versionBytes[3]);

            // Detect game and platform
            string game = "Unknown";
            string platform = "Unknown";

            if (VersionMap.TryGetValue(version, out var info))
            {
                game = info.Game;
                // Determine specific platform based on signed status
                platform = DetermineSpecificPlatform(info.Platforms, isSigned);
            }

            // Build info string
            string signedStr = isSigned ? "Signed" : "Unsigned";

            fileInfoLabel.Text = $"Header: {headerType} | {signedStr} | Studio: {studio} | Game: {game} | Platform: {platform} | Version: 0x{version:X}";
            fileInfoLabel.ForeColor = Color.DarkGreen;

            // Update detailed info
            UpdateDetailedInfo(fs.Length, headerStr, isSigned, studio, game, platform, version);
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

    /// <summary>
    /// Determines the specific platform based on available platforms and signed status.
    /// Signed files are Xbox 360, unsigned console files are PS3.
    /// </summary>
    private static string DetermineSpecificPlatform(string[] platforms, bool isSigned)
    {
        // If only one platform is possible, return it
        if (platforms.Length == 1)
            return platforms[0];

        // Check if this is a console-only version (PS3/Xbox 360)
        bool hasPS3 = platforms.Contains("PS3");
        bool hasXbox360 = platforms.Contains("Xbox 360");
        bool hasPC = platforms.Contains("PC");

        // If both PS3 and Xbox 360 are possibilities
        if (hasPS3 && hasXbox360)
        {
            // Signed = Xbox 360, Unsigned = PS3
            return isSigned ? "Xbox 360" : "PS3";
        }

        // If PC is in the mix with consoles (like WaW which has PS3/Xbox360/PC all with same version)
        if (hasPC && (hasPS3 || hasXbox360))
        {
            // For WaW-style games where all platforms share the same version:
            // - Signed = Xbox 360
            // - Unsigned could be PS3 or PC (can't distinguish without more info)
            // We'll assume unsigned console files, so return PS3
            // (PC files would typically be detected differently in practice)
            return isSigned ? "Xbox 360" : "PS3/PC";
        }

        // Fallback to joining all platforms
        return string.Join("/", platforms);
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
