using Call_of_Duty_FastFile_Editor.Constants;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    public static class UIManager
    {
        /// <summary>
        /// Sets the main window’s title bar to include the program name, version and the opened .ff path.
        /// </summary>
        public static void SetProgramTitle(this Form mainForm, string fastFilePath)
        {
            string version = ApplicationConstants.ProgramVersion;
            string programName = ApplicationConstants.ProgramName;
            mainForm.Text = $"{programName} - {version} - [{fastFilePath}]";
        }

        /// <summary>
        /// Sets the main window’s title bar to include the program name, version.
        /// </summary>
        public static void SetProgramTitle(this Form mainForm)
        {
            string version = ApplicationConstants.ProgramVersion;
            string programName = ApplicationConstants.ProgramName;
            mainForm.Text = $"{programName} - {version}";
        }

        public static void UpdateLoadedFileNameStatusStrip(ToolStripStatusLabel statusLabel, FastFile fastFile)
        {
            if (fastFile == null || string.IsNullOrEmpty(fastFile.FastFileName))
            {
                statusLabel.Visible = false;
                return;
            }

            // Decide the prefix based on the game type
            string gameString;
            if (fastFile.IsCod4File)
                gameString = "COD4";
            else if (fastFile.IsCod5File)
                gameString = "COD5";
            else if (fastFile.IsMW2File)
                gameString = "MW2";
            else if (fastFile.IsGhostsFile)
                gameString = "Ghosts";
            else
                gameString = "Unknown";

            statusLabel.Text = $"{gameString}: {fastFile.FastFileName}";
            statusLabel.Visible = true;
        }

        public static void UpdateSelectedFileStatusStrip(ToolStripStatusLabel statusLabel, string fileName)
        {
            if (fileName != null)
            {
                statusLabel.Text = fileName;
                statusLabel.Visible = true;
            }
        }

        public static void UpdateStatusStrip(ToolStripStatusLabel maxSizeLabel, ToolStripStatusLabel currentSizeLabel, int maxSize, int currentSize)
        {
            maxSizeLabel.Text = $"Max Size: {maxSize} (dec)";
            currentSizeLabel.Text = $"Current Size: {currentSize} (dec)";
            currentSizeLabel.ForeColor = currentSize > maxSize
                ? Color.Red
                : (ThemeManager.IsDark ? Color.White : Color.Black);
            maxSizeLabel.Visible = true;
            currentSizeLabel.Visible = true;
        }

        public static void SetRawFileTreeNodeColors(TreeView treeView)
        {
            SetNodeColorsRecursive(treeView.Nodes);
        }

        private static void SetNodeColorsRecursive(TreeNodeCollection nodes)
        {
            bool dark = ThemeManager.IsDark;

            foreach (TreeNode node in nodes)
            {
                // Apply colors based on file extension. Dark mode uses lighter, higher-
                // contrast variants so the labels stay readable on a dark background.
                if (node.Text.Contains(".cfg"))
                    node.ForeColor = dark ? Color.FromArgb(78, 201, 176) : Color.Teal;
                else if (node.Text.Contains(".gsc"))
                    node.ForeColor = dark ? Color.FromArgb(86, 156, 214) : Color.Blue;
                else if (node.Text.Contains(".atr"))
                    node.ForeColor = dark ? Color.FromArgb(115, 201, 145) : Color.Green;
                else if (node.Text.Contains(".vision"))
                    node.ForeColor = dark ? Color.FromArgb(197, 134, 192) : Color.DarkViolet;
                else if (node.Text.Contains(".rmb"))
                    node.ForeColor = dark ? Color.FromArgb(206, 145, 120) : Color.Brown;
                else if (node.Text.Contains(".csc"))
                    node.ForeColor = dark ? Color.FromArgb(244, 71, 71) : Color.Red;
                else
                    node.ForeColor = dark ? Color.FromArgb(241, 241, 241) : Color.Empty;

                // Recursively process child nodes (files inside folders)
                if (node.Nodes.Count > 0)
                {
                    SetNodeColorsRecursive(node.Nodes);
                }
            }
        }
    }
}