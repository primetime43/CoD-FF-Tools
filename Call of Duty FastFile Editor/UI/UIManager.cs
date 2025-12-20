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
            currentSizeLabel.ForeColor = currentSize > maxSize ? Color.Red : Color.Black;
            maxSizeLabel.Visible = true;
            currentSizeLabel.Visible = true;
        }

        public static void SetRawFileTreeNodeColors(TreeView treeView)
        {
            SetNodeColorsRecursive(treeView.Nodes);
        }

        private static void SetNodeColorsRecursive(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                // Apply colors based on file extension
                if (node.Text.Contains(".cfg"))
                {
                    node.ForeColor = Color.Black;
                }
                else if (node.Text.Contains(".gsc"))
                {
                    node.ForeColor = Color.Blue;
                }
                else if (node.Text.Contains(".atr"))
                {
                    node.ForeColor = Color.Green;
                }
                else if (node.Text.Contains(".vision"))
                {
                    node.ForeColor = Color.DarkViolet;
                }
                else if (node.Text.Contains(".rmb"))
                {
                    node.ForeColor = Color.Brown;
                }
                else if (node.Text.Contains(".csc"))
                {
                    node.ForeColor = Color.Red;
                }

                // Recursively process child nodes (files inside folders)
                if (node.Nodes.Count > 0)
                {
                    SetNodeColorsRecursive(node.Nodes);
                }
            }
        }
    }
}