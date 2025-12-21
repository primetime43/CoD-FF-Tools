using ICSharpCode.TextEditor;
using ICSharpCode.TextEditorEx;

namespace Call_of_Duty_FastFile_Editor
{
    partial class MainWindowForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            filesTreeView = new TreeView();
            contextMenuStripRawFiles = new ContextMenuStrip(components);
            exportFileMenuItem = new ToolStripMenuItem();
            exportContentOnlyMenuItem = new ToolStripMenuItem();
            renameFileToolStripMenuItem = new ToolStripMenuItem();
            increaseFileSizeToolStripMenuItem = new ToolStripMenuItem();
            deleteRawFileToolStripMenuItem = new ToolStripMenuItem();
            menuStripTopToolbar = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openFastFileToolStripMenuItem = new ToolStripMenuItem();
            openZoneFileToolStripMenuItem = new ToolStripMenuItem();
            COD5ToolStripMenuItem = new ToolStripMenuItem();
            cOD4ToolStripMenuItem = new ToolStripMenuItem();
            mW2ToolStripMenuItem = new ToolStripMenuItem();
            saveFastFileToolStripMenuItem = new ToolStripMenuItem();
            saveFastFileAsToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem1 = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            renameRawFileToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            toolStripSeparator3 = new ToolStripSeparator();
            toolStripSeparator4 = new ToolStripSeparator();
            toolStripSeparator5 = new ToolStripSeparator();
            compressCodeToolStripMenuItem = new ToolStripMenuItem();
            formatCodeToolStripMenuItem = new ToolStripMenuItem();
            removeCommentsToolStripMenuItem = new ToolStripMenuItem();
            checkSyntaxToolStripMenuItem = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            optionsToolStripMenuItem = new ToolStripMenuItem();
            fileInfoToolStripMenuItem = new ToolStripMenuItem();
            zoneHexViewToolStripMenuItem = new ToolStripMenuItem();
            rawFileToolsMenuItem = new ToolStripMenuItem();
            reloadRawFilesPatternMatchingToolStripMenuItem = new ToolStripMenuItem();
            increaseRawFileSizeToolStripMenuItem = new ToolStripMenuItem();
            transferSpaceToolStripMenuItem = new ToolStripMenuItem();
            searchRawFileTxtMenuItem = new ToolStripMenuItem();
            injectFileToolStripMenuItem = new ToolStripMenuItem();
            extractAllRawFilesToolStripMenuItem = new ToolStripMenuItem();
            localizeToolsMenuItem = new ToolStripMenuItem();
            exportLocalizeToTxtMenuItem = new ToolStripMenuItem();
            importLocalizeFromTxtMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            compressZoneToFFToolStripMenuItem = new ToolStripMenuItem();
            keepZoneFileToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            CheckForUpdatesToolStripMenuItem = new ToolStripMenuItem();
            splitContainer1 = new SplitContainer();
            textEditorControlEx1 = new TextEditorControlEx();
            statusStripBottom = new StatusStrip();
            loadedFileNameStatusLabel = new ToolStripStatusLabel();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            selectedItemStatusLabel = new ToolStripStatusLabel();
            selectedFileMaxSizeStatusLabel = new ToolStripStatusLabel();
            selectedFileCurrentSizeStatusLabel = new ToolStripStatusLabel();
            filesTreeToolTip = new ToolTip(components);
            mainTabControl = new TabControl();
            universalContextMenu = new ContextMenuStrip(components);
            copyToolStripMenuItem = new ToolStripMenuItem();
            rawFilesPage = new TabPage();
            collision_Map_AssetTabPage = new TabPage();
            treeViewMapEnt = new TreeView();
            localizeTabPage = new TabPage();
            localizeInstructionLabel = new Label();
            localizeListView = new ListView();
            menuFilesTabPage = new TabPage();
            menuFilesSplitContainer = new SplitContainer();
            menuFilesTreeView = new TreeView();
            menuFilesTextEditor = new TextEditorControlEx();
            menuFilesListView = new ListView();
            tagsTabPage = new TabPage();
            tagsListView = new ListView();
            assetPoolTabPage = new TabPage();
            assetPoolListView = new ListView();
            techSetsTabPage = new TabPage();
            techSetsListView = new ListView();
            xAnimsTabPage = new TabPage();
            xAnimsListView = new ListView();
            xAnimsContextMenu = new ContextMenuStrip(components);
            exportXAnimMenuItem = new ToolStripMenuItem();
            weaponsTabPage = new TabPage();
            weaponsInstructionLabel = new Label();
            weaponsListView = new ListView();
            weaponsContextMenu = new ContextMenuStrip(components);
            editWeaponMenuItem = new ToolStripMenuItem();
            imagesTabPage = new TabPage();
            imagesListView = new ListView();
            stringTablesTabPage = new TabPage();
            stringTablesInstructionLabel = new Label();
            stringTablesListView = new ListView();
            zoneFileTabPage = new TabPage();
            zoneInfoDataGridView = new DataGridView();
            bindingSource1 = new BindingSource(components);
            reportIssuesToolStripMenuItem = new ToolStripMenuItem();
            contextMenuStripRawFiles.SuspendLayout();
            menuStripTopToolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            statusStripBottom.SuspendLayout();
            mainTabControl.SuspendLayout();
            universalContextMenu.SuspendLayout();
            rawFilesPage.SuspendLayout();
            collision_Map_AssetTabPage.SuspendLayout();
            localizeTabPage.SuspendLayout();
            menuFilesTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)menuFilesSplitContainer).BeginInit();
            menuFilesSplitContainer.Panel1.SuspendLayout();
            menuFilesSplitContainer.Panel2.SuspendLayout();
            menuFilesSplitContainer.SuspendLayout();
            tagsTabPage.SuspendLayout();
            assetPoolTabPage.SuspendLayout();
            techSetsTabPage.SuspendLayout();
            xAnimsTabPage.SuspendLayout();
            xAnimsContextMenu.SuspendLayout();
            weaponsContextMenu.SuspendLayout();
            weaponsTabPage.SuspendLayout();
            imagesTabPage.SuspendLayout();
            stringTablesTabPage.SuspendLayout();
            zoneFileTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)zoneInfoDataGridView).BeginInit();
            ((System.ComponentModel.ISupportInitialize)bindingSource1).BeginInit();
            SuspendLayout();
            // 
            // filesTreeView
            // 
            filesTreeView.BackColor = SystemColors.ScrollBar;
            filesTreeView.ContextMenuStrip = contextMenuStripRawFiles;
            filesTreeView.Dock = DockStyle.Fill;
            filesTreeView.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            filesTreeView.HideSelection = false;
            filesTreeView.Location = new Point(8, 0);
            filesTreeView.Name = "filesTreeView";
            filesTreeView.Size = new Size(330, 743);
            filesTreeView.TabIndex = 0;
            filesTreeToolTip.SetToolTip(filesTreeView, "Right click for more options.");
            filesTreeView.BeforeSelect += filesTreeView_BeforeSelect;
            filesTreeView.AfterSelect += filesTreeView_AfterSelect;
            // 
            // contextMenuStripRawFiles
            // 
            contextMenuStripRawFiles.Items.AddRange(new ToolStripItem[] { exportFileMenuItem, exportContentOnlyMenuItem, renameFileToolStripMenuItem, increaseFileSizeToolStripMenuItem, deleteRawFileToolStripMenuItem });
            contextMenuStripRawFiles.Name = "contextMenuStrip1";
            contextMenuStripRawFiles.Size = new Size(200, 114);
            // 
            // exportFileMenuItem
            // 
            exportFileMenuItem.Name = "exportFileMenuItem";
            exportFileMenuItem.Size = new Size(199, 22);
            exportFileMenuItem.Text = "Export (With Header)";
            exportFileMenuItem.ToolTipText = "Export the selected raw file with zone header for re-injection";
            exportFileMenuItem.Click += exportFileMenuItem_Click;
            //
            // exportContentOnlyMenuItem
            //
            exportContentOnlyMenuItem.Name = "exportContentOnlyMenuItem";
            exportContentOnlyMenuItem.Size = new Size(199, 22);
            exportContentOnlyMenuItem.Text = "Export (Content Only)";
            exportContentOnlyMenuItem.ToolTipText = "Export only the script content without zone header";
            exportContentOnlyMenuItem.Click += exportContentOnlyMenuItem_Click;
            //
            // renameFileToolStripMenuItem
            // 
            renameFileToolStripMenuItem.Name = "renameFileToolStripMenuItem";
            renameFileToolStripMenuItem.Size = new Size(161, 22);
            renameFileToolStripMenuItem.Text = "Rename File";
            renameFileToolStripMenuItem.Click += renameFileToolStripMenuItem_Click;
            // 
            // increaseFileSizeToolStripMenuItem
            //
            increaseFileSizeToolStripMenuItem.Name = "increaseFileSizeToolStripMenuItem";
            increaseFileSizeToolStripMenuItem.Size = new Size(161, 22);
            increaseFileSizeToolStripMenuItem.Text = "Increase File Size";
            increaseFileSizeToolStripMenuItem.Click += increaseFileSizeToolStripMenuItem_Click;
            //
            // deleteRawFileToolStripMenuItem
            //
            deleteRawFileToolStripMenuItem.Name = "deleteRawFileToolStripMenuItem";
            deleteRawFileToolStripMenuItem.Size = new Size(161, 22);
            deleteRawFileToolStripMenuItem.Text = "Delete Raw File";
            deleteRawFileToolStripMenuItem.Click += deleteRawFileToolStripMenuItem_Click;
            //
            // menuStripTopToolbar
            // 
            menuStripTopToolbar.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            menuStripTopToolbar.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, viewToolStripMenuItem, toolsToolStripMenuItem, optionsToolStripMenuItem, helpToolStripMenuItem });
            menuStripTopToolbar.Location = new Point(0, 0);
            menuStripTopToolbar.Name = "menuStripTopToolbar";
            menuStripTopToolbar.Size = new Size(1450, 28);
            menuStripTopToolbar.TabIndex = 1;
            menuStripTopToolbar.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openFastFileToolStripMenuItem, openZoneFileToolStripMenuItem, toolStripSeparator3, saveFastFileToolStripMenuItem, saveFastFileAsToolStripMenuItem, toolStripSeparator4, toolStripMenuItem1, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(44, 24);
            fileToolStripMenuItem.Text = "File";
            // 
            // openFastFileToolStripMenuItem
            // 
            openFastFileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { COD5ToolStripMenuItem, cOD4ToolStripMenuItem, mW2ToolStripMenuItem });
            openFastFileToolStripMenuItem.Name = "openFastFileToolStripMenuItem";
            openFastFileToolStripMenuItem.Size = new Size(286, 24);
            openFastFileToolStripMenuItem.Text = "Open Fast File";
            //
            // openZoneFileToolStripMenuItem
            //
            openZoneFileToolStripMenuItem.Name = "openZoneFileToolStripMenuItem";
            openZoneFileToolStripMenuItem.Size = new Size(286, 24);
            openZoneFileToolStripMenuItem.Text = "Open Zone File...";
            openZoneFileToolStripMenuItem.ToolTipText = "Open a decompressed zone file directly";
            openZoneFileToolStripMenuItem.Click += openZoneFileToolStripMenuItem_Click;
            //
            // COD5ToolStripMenuItem
            // 
            COD5ToolStripMenuItem.Name = "COD5ToolStripMenuItem";
            COD5ToolStripMenuItem.Size = new Size(117, 24);
            COD5ToolStripMenuItem.Text = "COD5";
            COD5ToolStripMenuItem.Click += COD5ToolStripMenuItem_Click;
            // 
            // cOD4ToolStripMenuItem
            //
            cOD4ToolStripMenuItem.Name = "cOD4ToolStripMenuItem";
            cOD4ToolStripMenuItem.Size = new Size(117, 24);
            cOD4ToolStripMenuItem.Text = "COD4";
            cOD4ToolStripMenuItem.Click += cOD4ToolStripMenuItem_Click;
            //
            // mW2ToolStripMenuItem
            //
            mW2ToolStripMenuItem.Name = "mW2ToolStripMenuItem";
            mW2ToolStripMenuItem.Size = new Size(117, 24);
            mW2ToolStripMenuItem.Text = "MW2";
            mW2ToolStripMenuItem.Click += mW2ToolStripMenuItem_Click;
            //
            // saveFastFileToolStripMenuItem
            //
            saveFastFileToolStripMenuItem.Enabled = false;
            saveFastFileToolStripMenuItem.Name = "saveFastFileToolStripMenuItem";
            saveFastFileToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveFastFileToolStripMenuItem.Size = new Size(286, 24);
            saveFastFileToolStripMenuItem.Text = "Save FastFile";
            saveFastFileToolStripMenuItem.ToolTipText = "Saves all changes and compresses to .ff file";
            saveFastFileToolStripMenuItem.Click += saveFastFileToolStripMenuItem_Click;
            // 
            // saveFastFileAsToolStripMenuItem
            //
            saveFastFileAsToolStripMenuItem.Enabled = false;
            saveFastFileAsToolStripMenuItem.Name = "saveFastFileAsToolStripMenuItem";
            saveFastFileAsToolStripMenuItem.Size = new Size(286, 24);
            saveFastFileAsToolStripMenuItem.Text = "Save FastFile As...";
            saveFastFileAsToolStripMenuItem.ToolTipText = "Save to a new .ff file";
            saveFastFileAsToolStripMenuItem.Click += saveFastFileAsToolStripMenuItem_Click;
            // 
            // toolStripMenuItem1
            //
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(286, 24);
            toolStripMenuItem1.Text = "Close FastFile";
            toolStripMenuItem1.ToolTipText = "Close the currently opened FastFile";
            toolStripMenuItem1.Click += closeFastFileToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            //
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(286, 24);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.ToolTipText = "Exit the application";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { renameRawFileToolStripMenuItem, toolStripSeparator1, compressCodeToolStripMenuItem, formatCodeToolStripMenuItem, removeCommentsToolStripMenuItem, checkSyntaxToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(47, 24);
            editToolStripMenuItem.Text = "Edit";
            // 
            //
            // renameRawFileToolStripMenuItem
            // 
            renameRawFileToolStripMenuItem.Enabled = false;
            renameRawFileToolStripMenuItem.Name = "renameRawFileToolStripMenuItem";
            renameRawFileToolStripMenuItem.Size = new Size(214, 24);
            renameRawFileToolStripMenuItem.Text = "Rename Raw File";
            renameRawFileToolStripMenuItem.Click += renameRawFileToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(211, 6);
            // 
            // compressCodeToolStripMenuItem
            //
            compressCodeToolStripMenuItem.Name = "compressCodeToolStripMenuItem";
            compressCodeToolStripMenuItem.Size = new Size(214, 24);
            compressCodeToolStripMenuItem.Text = "Compress Code";
            compressCodeToolStripMenuItem.Click += compressCodeToolStripMenuItem_Click;
            //
            // formatCodeToolStripMenuItem
            //
            formatCodeToolStripMenuItem.Name = "formatCodeToolStripMenuItem";
            formatCodeToolStripMenuItem.Size = new Size(214, 24);
            formatCodeToolStripMenuItem.Text = "Format Code";
            formatCodeToolStripMenuItem.Click += formatCodeToolStripMenuItem_Click;
            //
            // removeCommentsToolStripMenuItem
            // 
            removeCommentsToolStripMenuItem.Name = "removeCommentsToolStripMenuItem";
            removeCommentsToolStripMenuItem.Size = new Size(214, 24);
            removeCommentsToolStripMenuItem.Text = "Remove Comments";
            removeCommentsToolStripMenuItem.Click += removeCommentsToolStripMenuItem_Click;
            // 
            // checkSyntaxToolStripMenuItem
            // 
            checkSyntaxToolStripMenuItem.Name = "checkSyntaxToolStripMenuItem";
            checkSyntaxToolStripMenuItem.Size = new Size(214, 24);
            checkSyntaxToolStripMenuItem.Text = "Check Syntax";
            checkSyntaxToolStripMenuItem.Click += checkSyntaxToolStripMenuItem_Click;
            // 
            // toolsToolStripMenuItem
            //
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { rawFileToolsMenuItem, localizeToolsMenuItem, toolStripSeparator2, compressZoneToFFToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(56, 24);
            toolsToolStripMenuItem.Text = "Tools";
            //
            // viewToolStripMenuItem
            //
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { fileInfoToolStripMenuItem, zoneHexViewToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(53, 24);
            viewToolStripMenuItem.Text = "View";
            //
            // fileInfoToolStripMenuItem
            //
            fileInfoToolStripMenuItem.Name = "fileInfoToolStripMenuItem";
            fileInfoToolStripMenuItem.Size = new Size(195, 24);
            fileInfoToolStripMenuItem.Text = "File Information...";
            fileInfoToolStripMenuItem.Enabled = false;
            fileInfoToolStripMenuItem.Click += new System.EventHandler(this.fileInfoToolStripMenuItem_Click);
            //
            // zoneHexViewToolStripMenuItem
            //
            zoneHexViewToolStripMenuItem.Name = "zoneHexViewToolStripMenuItem";
            zoneHexViewToolStripMenuItem.Size = new Size(195, 24);
            zoneHexViewToolStripMenuItem.Text = "Zone Hex View";
            zoneHexViewToolStripMenuItem.Click += zoneHexViewToolStripMenuItem_Click;
            //
            // optionsToolStripMenuItem
            //
            optionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { keepZoneFileToolStripMenuItem });
            optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            optionsToolStripMenuItem.Size = new Size(73, 24);
            optionsToolStripMenuItem.Text = "Options";
            //
            // rawFileToolsMenuItem
            //
            rawFileToolsMenuItem.DropDownItems.AddRange(new ToolStripItem[] { injectFileToolStripMenuItem, extractAllRawFilesToolStripMenuItem, toolStripSeparator5, increaseRawFileSizeToolStripMenuItem, transferSpaceToolStripMenuItem, searchRawFileTxtMenuItem, reloadRawFilesPatternMatchingToolStripMenuItem });
            rawFileToolsMenuItem.Name = "rawFileToolsMenuItem";
            rawFileToolsMenuItem.Size = new Size(210, 24);
            rawFileToolsMenuItem.Text = "Raw Files";
            //
            // transferSpaceToolStripMenuItem
            //
            transferSpaceToolStripMenuItem.Name = "transferSpaceToolStripMenuItem";
            transferSpaceToolStripMenuItem.Size = new Size(316, 24);
            transferSpaceToolStripMenuItem.Text = "Transfer Space Between Files...";
            transferSpaceToolStripMenuItem.ToolTipText = "Transfer allocated space from one raw file to another without losing content";
            transferSpaceToolStripMenuItem.Click += transferSpaceToolStripMenuItem_Click;
            //
            // toolStripSeparator5
            //
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(207, 6);
            // 
            // reloadRawFilesPatternMatchingToolStripMenuItem
            // 
            reloadRawFilesPatternMatchingToolStripMenuItem.Name = "reloadRawFilesPatternMatchingToolStripMenuItem";
            reloadRawFilesPatternMatchingToolStripMenuItem.Size = new Size(316, 24);
            reloadRawFilesPatternMatchingToolStripMenuItem.Text = "Reload Raw Files (Pattern Matching)";
            reloadRawFilesPatternMatchingToolStripMenuItem.ToolTipText = "Reload raw files using pattern matching.\nUse this option if some raw files do not appear or load correctly with the standard method.";
            reloadRawFilesPatternMatchingToolStripMenuItem.Click += reloadRawFilesPatternMatchingToolStripMenuItem_Click;
            // 
            // increaseRawFileSizeToolStripMenuItem
            //
            increaseRawFileSizeToolStripMenuItem.Name = "increaseRawFileSizeToolStripMenuItem";
            increaseRawFileSizeToolStripMenuItem.Size = new Size(316, 24);
            increaseRawFileSizeToolStripMenuItem.Text = "Increase Raw File Size";
            increaseRawFileSizeToolStripMenuItem.Click += increaseFileSizeToolStripMenuItem_Click;
            //
            // searchRawFileTxtMenuItem
            // 
            searchRawFileTxtMenuItem.Name = "searchRawFileTxtMenuItem";
            searchRawFileTxtMenuItem.Size = new Size(316, 24);
            searchRawFileTxtMenuItem.Text = "Search Raw File Text";
            searchRawFileTxtMenuItem.Click += searchRawFileTxtMenuItem_Click;
            //
            // injectFileToolStripMenuItem
            //
            injectFileToolStripMenuItem.Name = "injectFileToolStripMenuItem";
            injectFileToolStripMenuItem.Size = new Size(280, 24);
            injectFileToolStripMenuItem.Text = "Inject/Update File...";
            injectFileToolStripMenuItem.ToolTipText = "Update existing raw files only. Use the FF Compiler to add custom raw files.";
            injectFileToolStripMenuItem.Click += injectFileToolStripMenuItem_Click;
            //
            // extractAllRawFilesToolStripMenuItem
            //
            extractAllRawFilesToolStripMenuItem.Name = "extractAllRawFilesToolStripMenuItem";
            extractAllRawFilesToolStripMenuItem.Size = new Size(280, 24);
            extractAllRawFilesToolStripMenuItem.Text = "Extract All Files...";
            extractAllRawFilesToolStripMenuItem.ToolTipText = "Extracts file content without zone headers. Files can be edited and re-injected.";
            extractAllRawFilesToolStripMenuItem.Click += extractAllRawFilesToolStripMenuItem_Click;
            //
            // localizeToolsMenuItem
            //
            localizeToolsMenuItem.DropDownItems.AddRange(new ToolStripItem[] { exportLocalizeToTxtMenuItem, importLocalizeFromTxtMenuItem });
            localizeToolsMenuItem.Enabled = false;
            localizeToolsMenuItem.Name = "localizeToolsMenuItem";
            localizeToolsMenuItem.Size = new Size(210, 24);
            localizeToolsMenuItem.Text = "Localize";
            localizeToolsMenuItem.ToolTipText = "Export or import localize strings as .txt";
            //
            // exportLocalizeToTxtMenuItem
            //
            exportLocalizeToTxtMenuItem.Name = "exportLocalizeToTxtMenuItem";
            exportLocalizeToTxtMenuItem.Size = new Size(220, 24);
            exportLocalizeToTxtMenuItem.Text = "Export to Text...";
            exportLocalizeToTxtMenuItem.Click += exportLocalizeToTxtMenuItem_Click;
            //
            // importLocalizeFromTxtMenuItem
            //
            importLocalizeFromTxtMenuItem.Name = "importLocalizeFromTxtMenuItem";
            importLocalizeFromTxtMenuItem.Size = new Size(220, 24);
            importLocalizeFromTxtMenuItem.Text = "Import from Text...";
            importLocalizeFromTxtMenuItem.Click += importLocalizeFromTxtMenuItem_Click;
            //
            // toolStripSeparator2
            //
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(207, 6);
            //
            // compressZoneToFFToolStripMenuItem
            //
            compressZoneToFFToolStripMenuItem.Name = "compressZoneToFFToolStripMenuItem";
            compressZoneToFFToolStripMenuItem.Size = new Size(210, 24);
            compressZoneToFFToolStripMenuItem.Text = "Compress Zone to FastFile...";
            compressZoneToFFToolStripMenuItem.ToolTipText = "Compress an uncompressed .zone file to a .ff FastFile";
            compressZoneToFFToolStripMenuItem.Click += compressZoneToFFToolStripMenuItem_Click;
            //
            // keepZoneFileToolStripMenuItem
            //
            keepZoneFileToolStripMenuItem.CheckOnClick = true;
            keepZoneFileToolStripMenuItem.Name = "keepZoneFileToolStripMenuItem";
            keepZoneFileToolStripMenuItem.Size = new Size(210, 24);
            keepZoneFileToolStripMenuItem.Text = "Keep Uncompressed Zone File";
            keepZoneFileToolStripMenuItem.ToolTipText = "When checked, keeps a copy of the uncompressed .zone file when closing";
            //
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem, CheckForUpdatesToolStripMenuItem, reportIssuesToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(53, 24);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(195, 24);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // CheckForUpdatesToolStripMenuItem
            // 
            CheckForUpdatesToolStripMenuItem.Name = "CheckForUpdatesToolStripMenuItem";
            CheckForUpdatesToolStripMenuItem.Size = new Size(195, 24);
            CheckForUpdatesToolStripMenuItem.Text = "Check For Update";
            CheckForUpdatesToolStripMenuItem.Click += CheckForUpdatesToolStripMenuItem_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(3, 3);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(filesTreeView);
            splitContainer1.Panel1.Padding = new Padding(8, 0, 0, 0);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(textEditorControlEx1);
            splitContainer1.Size = new Size(1436, 743);
            splitContainer1.SplitterDistance = 338;
            splitContainer1.TabIndex = 0;
            // 
            // textEditorControlEx1
            // 
            textEditorControlEx1.ContextMenuEnabled = true;
            textEditorControlEx1.Dock = DockStyle.Fill;
            textEditorControlEx1.FoldingStrategy = "C#";
            textEditorControlEx1.Font = new Font("Courier New", 10F);
            textEditorControlEx1.Location = new Point(0, 0);
            textEditorControlEx1.Name = "textEditorControlEx1";
            textEditorControlEx1.Size = new Size(1094, 743);
            textEditorControlEx1.SyntaxHighlighting = "C#";
            textEditorControlEx1.TabIndex = 0;
            textEditorControlEx1.TextChanged += textEditorControlEx1_TextChanged;
            // 
            // statusStripBottom
            // 
            statusStripBottom.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            statusStripBottom.Items.AddRange(new ToolStripItem[] { loadedFileNameStatusLabel, toolStripStatusLabel1, selectedItemStatusLabel, selectedFileMaxSizeStatusLabel, selectedFileCurrentSizeStatusLabel });
            statusStripBottom.Location = new Point(0, 805);
            statusStripBottom.Name = "statusStripBottom";
            statusStripBottom.Size = new Size(1450, 22);
            statusStripBottom.TabIndex = 2;
            // 
            // loadedFileNameStatusLabel
            // 
            loadedFileNameStatusLabel.Name = "loadedFileNameStatusLabel";
            loadedFileNameStatusLabel.Size = new Size(117, 21);
            loadedFileNameStatusLabel.Text = "File Name Here";
            loadedFileNameStatusLabel.Visible = false;
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(0, 17);
            // 
            // selectedItemStatusLabel
            // 
            selectedItemStatusLabel.Margin = new Padding(100, 3, 0, 2);
            selectedItemStatusLabel.Name = "selectedItemStatusLabel";
            selectedItemStatusLabel.Size = new Size(140, 21);
            selectedItemStatusLabel.Text = "Selected Item Here";
            selectedItemStatusLabel.Visible = false;
            // 
            // selectedFileMaxSizeStatusLabel
            // 
            selectedFileMaxSizeStatusLabel.Margin = new Padding(100, 3, 0, 2);
            selectedFileMaxSizeStatusLabel.Name = "selectedFileMaxSizeStatusLabel";
            selectedFileMaxSizeStatusLabel.Size = new Size(161, 21);
            selectedFileMaxSizeStatusLabel.Text = "Selected File Max Size";
            selectedFileMaxSizeStatusLabel.Visible = false;
            // 
            // selectedFileCurrentSizeStatusLabel
            // 
            selectedFileCurrentSizeStatusLabel.Margin = new Padding(50, 3, 0, 2);
            selectedFileCurrentSizeStatusLabel.Name = "selectedFileCurrentSizeStatusLabel";
            selectedFileCurrentSizeStatusLabel.Size = new Size(185, 21);
            selectedFileCurrentSizeStatusLabel.Text = "Selected File Current Size";
            selectedFileCurrentSizeStatusLabel.Visible = false;
            //
            // filesTreeToolTip
            //
            filesTreeToolTip.ShowAlways = true;
            filesTreeToolTip.ToolTipIcon = ToolTipIcon.Info;
            //
            // mainTabControl
            // 
            mainTabControl.ContextMenuStrip = universalContextMenu;
            mainTabControl.Controls.Add(rawFilesPage);
            mainTabControl.Controls.Add(collision_Map_AssetTabPage);
            mainTabControl.Controls.Add(localizeTabPage);
            mainTabControl.Controls.Add(menuFilesTabPage);
            mainTabControl.Controls.Add(techSetsTabPage);
            mainTabControl.Controls.Add(xAnimsTabPage);
            mainTabControl.Controls.Add(weaponsTabPage);
            mainTabControl.Controls.Add(imagesTabPage);
            mainTabControl.Controls.Add(stringTablesTabPage);
            mainTabControl.Controls.Add(tagsTabPage);
            mainTabControl.Controls.Add(assetPoolTabPage);
            mainTabControl.Controls.Add(zoneFileTabPage);
            mainTabControl.Dock = DockStyle.Fill;
            mainTabControl.Location = new Point(0, 28);
            mainTabControl.Name = "mainTabControl";
            mainTabControl.SelectedIndex = 0;
            mainTabControl.Size = new Size(1450, 777);
            mainTabControl.TabIndex = 3;
            // 
            // universalContextMenu
            // 
            universalContextMenu.Items.AddRange(new ToolStripItem[] { copyToolStripMenuItem });
            universalContextMenu.Name = "contextMenuStripTagsCopy";
            universalContextMenu.Size = new Size(103, 26);
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new Size(102, 22);
            copyToolStripMenuItem.Text = "Copy";
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click;
            // 
            // rawFilesPage
            // 
            rawFilesPage.Controls.Add(splitContainer1);
            rawFilesPage.Location = new Point(4, 24);
            rawFilesPage.Name = "rawFilesPage";
            rawFilesPage.Padding = new Padding(3);
            rawFilesPage.Size = new Size(1442, 749);
            rawFilesPage.TabIndex = 0;
            rawFilesPage.Text = "Raw Files";
            rawFilesPage.UseVisualStyleBackColor = true;
            //
            // collision_Map_AssetTabPage
            // 
            collision_Map_AssetTabPage.Controls.Add(treeViewMapEnt);
            collision_Map_AssetTabPage.Location = new Point(4, 24);
            collision_Map_AssetTabPage.Name = "collision_Map_AssetTabPage";
            collision_Map_AssetTabPage.Padding = new Padding(3);
            collision_Map_AssetTabPage.Size = new Size(1442, 749);
            collision_Map_AssetTabPage.TabIndex = 4;
            collision_Map_AssetTabPage.Text = "Collision Map Data";
            collision_Map_AssetTabPage.UseVisualStyleBackColor = true;
            // 
            // treeViewMapEnt
            // 
            treeViewMapEnt.ContextMenuStrip = universalContextMenu;
            treeViewMapEnt.Location = new Point(0, 0);
            treeViewMapEnt.Name = "treeViewMapEnt";
            treeViewMapEnt.Size = new Size(307, 749);
            treeViewMapEnt.TabIndex = 2;
            treeViewMapEnt.MouseDown += treeView_MouseDownCopy;
            //
            // localizeTabPage
            //
            localizeTabPage.Controls.Add(localizeListView);
            localizeTabPage.Controls.Add(localizeInstructionLabel);
            localizeTabPage.Location = new Point(4, 24);
            localizeTabPage.Name = "localizeTabPage";
            localizeTabPage.Padding = new Padding(3);
            localizeTabPage.Size = new Size(1442, 749);
            localizeTabPage.TabIndex = 6;
            localizeTabPage.Text = "Localize";
            localizeTabPage.UseVisualStyleBackColor = true;
            //
            // localizeInstructionLabel
            //
            localizeInstructionLabel.AutoSize = true;
            localizeInstructionLabel.Dock = DockStyle.Top;
            localizeInstructionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            localizeInstructionLabel.ForeColor = SystemColors.GrayText;
            localizeInstructionLabel.Location = new Point(3, 3);
            localizeInstructionLabel.Name = "localizeInstructionLabel";
            localizeInstructionLabel.Padding = new Padding(0, 4, 0, 4);
            localizeInstructionLabel.Size = new Size(300, 23);
            localizeInstructionLabel.TabIndex = 1;
            localizeInstructionLabel.Text = "Double-click an item to edit its value.";
            //
            // localizeListView
            //
            localizeListView.ContextMenuStrip = universalContextMenu;
            localizeListView.Dock = DockStyle.Fill;
            localizeListView.Location = new Point(3, 26);
            localizeListView.Name = "localizeListView";
            localizeListView.Size = new Size(1436, 720);
            localizeListView.TabIndex = 0;
            localizeListView.UseCompatibleStateImageBehavior = false;
            localizeListView.MouseDown += listView_MouseDownCopy;
            //
            // menuFilesTabPage
            //
            menuFilesTabPage.Controls.Add(menuFilesSplitContainer);
            menuFilesTabPage.Location = new Point(4, 24);
            menuFilesTabPage.Name = "menuFilesTabPage";
            menuFilesTabPage.Padding = new Padding(3);
            menuFilesTabPage.Size = new Size(1442, 749);
            menuFilesTabPage.TabIndex = 7;
            menuFilesTabPage.Text = "Menu Files";
            menuFilesTabPage.UseVisualStyleBackColor = true;
            //
            // menuFilesSplitContainer
            //
            menuFilesSplitContainer.Dock = DockStyle.Fill;
            menuFilesSplitContainer.Location = new Point(3, 3);
            menuFilesSplitContainer.Name = "menuFilesSplitContainer";
            menuFilesSplitContainer.Panel1.Controls.Add(menuFilesTreeView);
            menuFilesSplitContainer.Panel2.Controls.Add(menuFilesTextEditor);
            menuFilesSplitContainer.Size = new Size(1436, 743);
            menuFilesSplitContainer.SplitterDistance = 350;
            menuFilesSplitContainer.TabIndex = 0;
            //
            // menuFilesTreeView
            //
            menuFilesTreeView.Dock = DockStyle.Fill;
            menuFilesTreeView.Location = new Point(0, 0);
            menuFilesTreeView.Name = "menuFilesTreeView";
            menuFilesTreeView.Size = new Size(350, 743);
            menuFilesTreeView.TabIndex = 0;
            //
            // menuFilesTextEditor
            //
            menuFilesTextEditor.ContextMenuEnabled = true;
            menuFilesTextEditor.Dock = DockStyle.Fill;
            menuFilesTextEditor.FoldingStrategy = "C#";
            menuFilesTextEditor.Font = new Font("Courier New", 10F);
            menuFilesTextEditor.Location = new Point(0, 0);
            menuFilesTextEditor.Name = "menuFilesTextEditor";
            menuFilesTextEditor.Size = new Size(1082, 743);
            menuFilesTextEditor.SyntaxHighlighting = "C#";
            menuFilesTextEditor.TabIndex = 0;
            //
            // menuFilesListView (kept for compatibility)
            //
            menuFilesListView.ContextMenuStrip = universalContextMenu;
            menuFilesListView.Dock = DockStyle.Fill;
            menuFilesListView.FullRowSelect = true;
            menuFilesListView.Location = new Point(3, 3);
            menuFilesListView.Name = "menuFilesListView";
            menuFilesListView.Size = new Size(1436, 743);
            menuFilesListView.TabIndex = 0;
            menuFilesListView.UseCompatibleStateImageBehavior = false;
            menuFilesListView.View = View.Details;
            menuFilesListView.MouseDown += listView_MouseDownCopy;
            //
            // tagsTabPage
            // 
            tagsTabPage.Controls.Add(tagsListView);
            tagsTabPage.Location = new Point(4, 24);
            tagsTabPage.Name = "tagsTabPage";
            tagsTabPage.Padding = new Padding(3);
            tagsTabPage.Size = new Size(1442, 749);
            tagsTabPage.TabIndex = 2;
            tagsTabPage.Text = "Tags";
            tagsTabPage.UseVisualStyleBackColor = true;
            // 
            // tagsListView
            // 
            tagsListView.ContextMenuStrip = universalContextMenu;
            tagsListView.Location = new Point(0, 0);
            tagsListView.Name = "tagsListView";
            tagsListView.Size = new Size(487, 750);
            tagsListView.TabIndex = 0;
            tagsListView.UseCompatibleStateImageBehavior = false;
            tagsListView.View = View.Details;
            tagsListView.MouseDown += listView_MouseDownCopy;
            //
            // assetPoolTabPage
            //
            assetPoolTabPage.Controls.Add(assetPoolListView);
            assetPoolTabPage.Location = new Point(4, 24);
            assetPoolTabPage.Name = "assetPoolTabPage";
            assetPoolTabPage.Padding = new Padding(3);
            assetPoolTabPage.Size = new Size(1442, 749);
            assetPoolTabPage.TabIndex = 5;
            assetPoolTabPage.Text = "Asset Pool";
            assetPoolTabPage.UseVisualStyleBackColor = true;
            //
            // assetPoolListView
            //
            assetPoolListView.ContextMenuStrip = universalContextMenu;
            assetPoolListView.Dock = DockStyle.Fill;
            assetPoolListView.Location = new Point(3, 3);
            assetPoolListView.Name = "assetPoolListView";
            assetPoolListView.Size = new Size(1436, 743);
            assetPoolListView.TabIndex = 0;
            assetPoolListView.UseCompatibleStateImageBehavior = false;
            assetPoolListView.MouseDown += listView_MouseDownCopy;
            //
            // techSetsTabPage
            //
            techSetsTabPage.Controls.Add(techSetsListView);
            techSetsTabPage.Location = new Point(4, 24);
            techSetsTabPage.Name = "techSetsTabPage";
            techSetsTabPage.Padding = new Padding(3);
            techSetsTabPage.Size = new Size(1442, 749);
            techSetsTabPage.TabIndex = 8;
            techSetsTabPage.Text = "TechSets";
            techSetsTabPage.UseVisualStyleBackColor = true;
            //
            // techSetsListView
            //
            techSetsListView.ContextMenuStrip = universalContextMenu;
            techSetsListView.Dock = DockStyle.Fill;
            techSetsListView.FullRowSelect = true;
            techSetsListView.Location = new Point(3, 3);
            techSetsListView.Name = "techSetsListView";
            techSetsListView.Size = new Size(1436, 743);
            techSetsListView.TabIndex = 0;
            techSetsListView.UseCompatibleStateImageBehavior = false;
            techSetsListView.View = View.Details;
            techSetsListView.MouseDown += listView_MouseDownCopy;
            //
            // xAnimsTabPage
            //
            xAnimsTabPage.Controls.Add(xAnimsListView);
            xAnimsTabPage.Location = new Point(4, 24);
            xAnimsTabPage.Name = "xAnimsTabPage";
            xAnimsTabPage.Padding = new Padding(3);
            xAnimsTabPage.Size = new Size(1442, 749);
            xAnimsTabPage.TabIndex = 10;
            xAnimsTabPage.Text = "XAnims";
            xAnimsTabPage.UseVisualStyleBackColor = true;
            //
            // xAnimsListView
            //
            xAnimsListView.ContextMenuStrip = xAnimsContextMenu;
            xAnimsListView.Dock = DockStyle.Fill;
            xAnimsListView.FullRowSelect = true;
            xAnimsListView.Location = new Point(3, 3);
            xAnimsListView.Name = "xAnimsListView";
            xAnimsListView.Size = new Size(1436, 743);
            xAnimsListView.TabIndex = 0;
            xAnimsListView.UseCompatibleStateImageBehavior = false;
            xAnimsListView.View = View.Details;
            xAnimsListView.MouseDown += listView_MouseDownCopy;
            //
            // xAnimsContextMenu
            //
            xAnimsContextMenu.Items.AddRange(new ToolStripItem[] { exportXAnimMenuItem });
            xAnimsContextMenu.Name = "xAnimsContextMenu";
            xAnimsContextMenu.Size = new Size(180, 26);
            //
            // exportXAnimMenuItem
            //
            exportXAnimMenuItem.Name = "exportXAnimMenuItem";
            exportXAnimMenuItem.Size = new Size(179, 22);
            exportXAnimMenuItem.Text = "Export XAnim Data...";
            exportXAnimMenuItem.Click += exportXAnimMenuItem_Click;
            //
            // weaponsContextMenu
            //
            weaponsContextMenu.Items.AddRange(new ToolStripItem[] { editWeaponMenuItem });
            weaponsContextMenu.Name = "weaponsContextMenu";
            weaponsContextMenu.Size = new Size(180, 26);
            //
            // editWeaponMenuItem
            //
            editWeaponMenuItem.Name = "editWeaponMenuItem";
            editWeaponMenuItem.Size = new Size(179, 22);
            editWeaponMenuItem.Text = "Edit Weapon...";
            editWeaponMenuItem.Click += editWeaponMenuItem_Click;
            //
            // weaponsTabPage
            //
            weaponsTabPage.Controls.Add(weaponsListView);
            weaponsTabPage.Controls.Add(weaponsInstructionLabel);
            weaponsTabPage.Location = new Point(4, 24);
            weaponsTabPage.Name = "weaponsTabPage";
            weaponsTabPage.Padding = new Padding(3);
            weaponsTabPage.Size = new Size(1442, 749);
            weaponsTabPage.TabIndex = 12;
            weaponsTabPage.Text = "Weapons";
            weaponsTabPage.UseVisualStyleBackColor = true;
            //
            // weaponsInstructionLabel
            //
            weaponsInstructionLabel.AutoSize = true;
            weaponsInstructionLabel.Dock = DockStyle.Top;
            weaponsInstructionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            weaponsInstructionLabel.ForeColor = SystemColors.GrayText;
            weaponsInstructionLabel.Location = new Point(3, 3);
            weaponsInstructionLabel.Name = "weaponsInstructionLabel";
            weaponsInstructionLabel.Padding = new Padding(0, 4, 0, 4);
            weaponsInstructionLabel.Size = new Size(400, 23);
            weaponsInstructionLabel.TabIndex = 1;
            weaponsInstructionLabel.Text = "Double-click a weapon or right-click and select 'Edit Weapon...' to modify values.";
            //
            // weaponsListView
            //
            weaponsListView.ContextMenuStrip = weaponsContextMenu;
            weaponsListView.Dock = DockStyle.Fill;
            weaponsListView.FullRowSelect = true;
            weaponsListView.Location = new Point(3, 26);
            weaponsListView.Name = "weaponsListView";
            weaponsListView.Size = new Size(1436, 720);
            weaponsListView.TabIndex = 0;
            weaponsListView.UseCompatibleStateImageBehavior = false;
            weaponsListView.View = View.Details;
            weaponsListView.DoubleClick += weaponsListView_DoubleClick;
            weaponsListView.MouseDown += listView_MouseDownCopy;
            //
            // imagesTabPage
            //
            imagesTabPage.Controls.Add(imagesListView);
            imagesTabPage.Location = new Point(4, 24);
            imagesTabPage.Name = "imagesTabPage";
            imagesTabPage.Padding = new Padding(3);
            imagesTabPage.Size = new Size(1442, 749);
            imagesTabPage.TabIndex = 13;
            imagesTabPage.Text = "Images";
            imagesTabPage.UseVisualStyleBackColor = true;
            //
            // imagesListView
            //
            imagesListView.Dock = DockStyle.Fill;
            imagesListView.FullRowSelect = true;
            imagesListView.Location = new Point(3, 3);
            imagesListView.Name = "imagesListView";
            imagesListView.Size = new Size(1436, 743);
            imagesListView.TabIndex = 0;
            imagesListView.UseCompatibleStateImageBehavior = false;
            imagesListView.View = View.Details;
            imagesListView.MouseDown += listView_MouseDownCopy;
            imagesListView.DoubleClick += imagesListView_DoubleClick;
            //
            // stringTablesTabPage
            //
            stringTablesTabPage.Controls.Add(stringTablesListView);
            stringTablesTabPage.Controls.Add(stringTablesInstructionLabel);
            stringTablesTabPage.Location = new Point(4, 24);
            stringTablesTabPage.Name = "stringTablesTabPage";
            stringTablesTabPage.Padding = new Padding(3);
            stringTablesTabPage.Size = new Size(1442, 749);
            stringTablesTabPage.TabIndex = 11;
            stringTablesTabPage.Text = "String Tables";
            stringTablesTabPage.UseVisualStyleBackColor = true;
            //
            // stringTablesInstructionLabel
            //
            stringTablesInstructionLabel.AutoSize = true;
            stringTablesInstructionLabel.Dock = DockStyle.Top;
            stringTablesInstructionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            stringTablesInstructionLabel.ForeColor = SystemColors.GrayText;
            stringTablesInstructionLabel.Location = new Point(3, 3);
            stringTablesInstructionLabel.Name = "stringTablesInstructionLabel";
            stringTablesInstructionLabel.Padding = new Padding(0, 4, 0, 4);
            stringTablesInstructionLabel.Size = new Size(350, 23);
            stringTablesInstructionLabel.TabIndex = 1;
            stringTablesInstructionLabel.Text = "Double-click a string table to view its contents.";
            //
            // stringTablesListView
            //
            stringTablesListView.ContextMenuStrip = universalContextMenu;
            stringTablesListView.Dock = DockStyle.Fill;
            stringTablesListView.FullRowSelect = true;
            stringTablesListView.Location = new Point(3, 26);
            stringTablesListView.Name = "stringTablesListView";
            stringTablesListView.Size = new Size(1436, 720);
            stringTablesListView.TabIndex = 0;
            stringTablesListView.UseCompatibleStateImageBehavior = false;
            stringTablesListView.View = View.Details;
            stringTablesListView.MouseDown += listView_MouseDownCopy;
            stringTablesListView.DoubleClick += stringTablesListView_DoubleClick;
            //
            // zoneFileTabPage
            //
            zoneFileTabPage.Controls.Add(zoneInfoDataGridView);
            zoneFileTabPage.Location = new Point(4, 24);
            zoneFileTabPage.Name = "zoneFileTabPage";
            zoneFileTabPage.Padding = new Padding(3);
            zoneFileTabPage.Size = new Size(1442, 749);
            zoneFileTabPage.TabIndex = 1;
            zoneFileTabPage.Text = "Zone Header";
            zoneFileTabPage.UseVisualStyleBackColor = true;
            // 
            // zoneInfoDataGridView
            // 
            zoneInfoDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            zoneInfoDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            zoneInfoDataGridView.Location = new Point(-4, 0);
            zoneInfoDataGridView.Name = "zoneInfoDataGridView";
            zoneInfoDataGridView.Size = new Size(493, 432);
            zoneInfoDataGridView.TabIndex = 0;
            zoneInfoDataGridView.MouseDown += dataGrid_MouseDownCopy;
            // 
            // reportIssuesToolStripMenuItem
            // 
            reportIssuesToolStripMenuItem.Name = "reportIssuesToolStripMenuItem";
            reportIssuesToolStripMenuItem.Size = new Size(195, 24);
            reportIssuesToolStripMenuItem.Text = "Report Issues";
            reportIssuesToolStripMenuItem.Click += reportIssuesToolStripMenuItem_Click;
            // 
            // MainWindowForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1450, 827);
            Controls.Add(mainTabControl);
            Controls.Add(statusStripBottom);
            Controls.Add(menuStripTopToolbar);
            MainMenuStrip = menuStripTopToolbar;
            Name = "MainWindowForm";
            contextMenuStripRawFiles.ResumeLayout(false);
            menuStripTopToolbar.ResumeLayout(false);
            menuStripTopToolbar.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            statusStripBottom.ResumeLayout(false);
            statusStripBottom.PerformLayout();
            mainTabControl.ResumeLayout(false);
            universalContextMenu.ResumeLayout(false);
            rawFilesPage.ResumeLayout(false);
            collision_Map_AssetTabPage.ResumeLayout(false);
            localizeTabPage.ResumeLayout(false);
            menuFilesSplitContainer.Panel1.ResumeLayout(false);
            menuFilesSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)menuFilesSplitContainer).EndInit();
            menuFilesSplitContainer.ResumeLayout(false);
            menuFilesTabPage.ResumeLayout(false);
            tagsTabPage.ResumeLayout(false);
            assetPoolTabPage.ResumeLayout(false);
            techSetsTabPage.ResumeLayout(false);
            xAnimsTabPage.ResumeLayout(false);
            xAnimsContextMenu.ResumeLayout(false);
            weaponsContextMenu.ResumeLayout(false);
            weaponsTabPage.ResumeLayout(false);
            imagesTabPage.ResumeLayout(false);
            stringTablesTabPage.ResumeLayout(false);
            zoneFileTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)zoneInfoDataGridView).EndInit();
            ((System.ComponentModel.ISupportInitialize)bindingSource1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TreeView filesTreeView;
        private MenuStrip menuStripTopToolbar;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openFastFileToolStripMenuItem;
        private SplitContainer splitContainer1;
        private StatusStrip statusStripBottom;
        private ToolStripStatusLabel loadedFileNameStatusLabel;
        private ToolStripStatusLabel selectedItemStatusLabel;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private ToolStripStatusLabel selectedFileMaxSizeStatusLabel;
        private ToolStripStatusLabel selectedFileCurrentSizeStatusLabel;
        private TextEditorControlEx textEditorControlEx1;
        private ToolStripMenuItem saveFastFileToolStripMenuItem;
        private ToolStripMenuItem saveFastFileAsToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem toolsToolStripMenuItem;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem optionsToolStripMenuItem;
        private ToolStripMenuItem fileInfoToolStripMenuItem;
        private ToolStripMenuItem zoneHexViewToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem renameRawFileToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem compressCodeToolStripMenuItem;
        private ToolStripMenuItem formatCodeToolStripMenuItem;
        private ToolStripMenuItem removeCommentsToolStripMenuItem;
        private ToolStripMenuItem checkSyntaxToolStripMenuItem;
        private ToolStripMenuItem searchRawFileTxtMenuItem;
        private ToolStripMenuItem injectFileToolStripMenuItem;
        private ToolStripMenuItem localizeToolsMenuItem;
        private ToolStripMenuItem exportLocalizeToTxtMenuItem;
        private ToolStripMenuItem importLocalizeFromTxtMenuItem;
        private ContextMenuStrip contextMenuStripRawFiles;
        private ToolStripMenuItem exportFileMenuItem;
        private ToolStripMenuItem exportContentOnlyMenuItem;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolTip filesTreeToolTip;
        private ToolStripMenuItem renameFileToolStripMenuItem;
        private TabControl mainTabControl;
        private TabPage rawFilesPage;
        private TabPage zoneFileTabPage;
        private DataGridView zoneInfoDataGridView;
        private BindingSource bindingSource1;
        private TabPage tagsTabPage;
        private ListView tagsListView;
        private ContextMenuStrip universalContextMenu;
        private ToolStripMenuItem copyToolStripMenuItem;
        private TabPage collision_Map_AssetTabPage;
        private TreeView treeViewMapEnt;
        private ToolStripMenuItem rawFileToolsMenuItem;
        private ToolStripMenuItem increaseRawFileSizeToolStripMenuItem;
        private ToolStripMenuItem transferSpaceToolStripMenuItem;
        private TabPage assetPoolTabPage;
        private ListView assetPoolListView;
        private TabPage techSetsTabPage;
        private ListView techSetsListView;
        private TabPage xAnimsTabPage;
        private ListView xAnimsListView;
        private ContextMenuStrip xAnimsContextMenu;
        private ToolStripMenuItem exportXAnimMenuItem;
        private TabPage weaponsTabPage;
        private Label weaponsInstructionLabel;
        private ListView weaponsListView;
        private ContextMenuStrip weaponsContextMenu;
        private ToolStripMenuItem editWeaponMenuItem;
        private TabPage imagesTabPage;
        private ListView imagesListView;
        private TabPage stringTablesTabPage;
        private Label stringTablesInstructionLabel;
        private ListView stringTablesListView;
        private TabPage localizeTabPage;
        private Label localizeInstructionLabel;
        private ListView localizeListView;
        private TabPage menuFilesTabPage;
        private SplitContainer menuFilesSplitContainer;
        private TreeView menuFilesTreeView;
        private TextEditorControlEx menuFilesTextEditor;
        private ListView menuFilesListView;
        private ToolStripMenuItem extractAllRawFilesToolStripMenuItem;
        private ToolStripMenuItem increaseFileSizeToolStripMenuItem;
        private ToolStripMenuItem deleteRawFileToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem CheckForUpdatesToolStripMenuItem;
        private ToolStripMenuItem COD5ToolStripMenuItem;
        private ToolStripMenuItem cOD4ToolStripMenuItem;
        private ToolStripMenuItem mW2ToolStripMenuItem;
        private ToolStripMenuItem reloadRawFilesPatternMatchingToolStripMenuItem;
        private ToolStripMenuItem reportIssuesToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem compressZoneToFFToolStripMenuItem;
        private ToolStripMenuItem openZoneFileToolStripMenuItem;
        private ToolStripMenuItem keepZoneFileToolStripMenuItem;
    }
}
