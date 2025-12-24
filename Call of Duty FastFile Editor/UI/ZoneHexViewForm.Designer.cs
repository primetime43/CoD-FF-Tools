using Be.Windows.Forms;

namespace Call_of_Duty_FastFile_Editor.UI
{
    partial class ZoneHexViewForm
    {
        private System.ComponentModel.IContainer components = null;

        // menus
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem,
                                    saveAsToolStripMenuItem,
                                    closeToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem,
                                    copyHexToolStripMenuItem,
                                    copyAsciiToolStripMenuItem,
                                    selectAllToolStripMenuItem;
        private ToolStripMenuItem goToToolStripMenuItem;
        private ToolStripMenuItem byteOrderMenu,
                                    littleEndianItem,
                                    bigEndianItem;
        private ToolStripMenuItem viewToolStripMenuItem,
                                    showAssetPanelMenuItem,
                                    highlightAssetsMenuItem;

        // status bar
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel offsetStatusLabel,
                                    selStatusLabel,
                                    valueStatusLabel,
                                    assetStatusLabel;

        // main controls
        internal HexBox hexBox;
        private TextBox inspectorTextBox;

        // asset panel
        private SplitContainer mainSplitContainer;
        private Panel assetPanel;
        private Label assetPanelLabel;
        private Panel assetSearchPanel;
        private TextBox assetSearchTextBox;
        private ListView assetListView;
        private ColumnHeader assetTypeColumn;
        private ColumnHeader assetNameColumn;
        private ColumnHeader assetStartColumn;
        private ColumnHeader assetEndColumn;
        private Button jumpToAssetButton;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // MenuStrip
            this.menuStrip1 = new MenuStrip();

            // File
            this.fileToolStripMenuItem = new ToolStripMenuItem("File");
            this.saveAsToolStripMenuItem = new ToolStripMenuItem("Save As...");
            this.closeToolStripMenuItem = new ToolStripMenuItem("Close");
            this.fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]{
                saveAsToolStripMenuItem,
                closeToolStripMenuItem
            });

            // Edit
            this.editToolStripMenuItem = new ToolStripMenuItem("Edit");
            this.copyHexToolStripMenuItem = new ToolStripMenuItem("Copy Hex");
            this.copyAsciiToolStripMenuItem = new ToolStripMenuItem("Copy ASCII");
            this.selectAllToolStripMenuItem = new ToolStripMenuItem("Select All");
            this.editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]{
                copyHexToolStripMenuItem,
                copyAsciiToolStripMenuItem,
                selectAllToolStripMenuItem
            });

            // Go To...
            this.goToToolStripMenuItem = new ToolStripMenuItem("Go To...");

            // Byte Order submenu
            this.byteOrderMenu = new ToolStripMenuItem("Byte Order");
            this.littleEndianItem = new ToolStripMenuItem("Little-endian") { Checked = false, CheckOnClick = true };
            this.bigEndianItem = new ToolStripMenuItem("Big-endian") { Checked = true, CheckOnClick = true };
            this.byteOrderMenu.DropDownItems.AddRange(new ToolStripItem[]{
                littleEndianItem,
                bigEndianItem
            });
            // attach Byte Order into Edit
            this.editToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            this.editToolStripMenuItem.DropDownItems.Add(byteOrderMenu);

            // View menu
            this.viewToolStripMenuItem = new ToolStripMenuItem("View");
            this.showAssetPanelMenuItem = new ToolStripMenuItem("Asset Panel") { Checked = true, CheckOnClick = true };
            this.highlightAssetsMenuItem = new ToolStripMenuItem("Highlight Selected Asset") { Checked = true, CheckOnClick = true };
            this.viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]{
                showAssetPanelMenuItem,
                highlightAssetsMenuItem
            });

            // top-level
            this.menuStrip1.Items.AddRange(new ToolStripItem[]{
                fileToolStripMenuItem,
                editToolStripMenuItem,
                viewToolStripMenuItem,
                goToToolStripMenuItem
            });

            // StatusStrip
            this.statusStrip1 = new StatusStrip();
            this.offsetStatusLabel = new ToolStripStatusLabel("Offset: 0x0");
            this.selStatusLabel = new ToolStripStatusLabel("Sel: 0 bytes");
            this.valueStatusLabel = new ToolStripStatusLabel("Value: --");
            this.assetStatusLabel = new ToolStripStatusLabel("Asset: --");
            this.statusStrip1.Items.AddRange(new ToolStripItem[]{
                offsetStatusLabel,
                new ToolStripSeparator(),
                selStatusLabel,
                new ToolStripSeparator(),
                valueStatusLabel,
                new ToolStripSeparator(),
                assetStatusLabel
            });

            // HexBox
            this.hexBox = new HexBox
            {
                Dock = DockStyle.Fill
            };

            // Inspector panel
            this.inspectorTextBox = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F)
            };

            // Asset Panel
            this.assetPanel = new Panel
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(250, 0)
            };

            this.assetPanelLabel = new Label
            {
                Text = "Asset Pool",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            this.assetSearchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28
            };

            this.assetSearchTextBox = new TextBox
            {
                Location = new Point(3, 3),
                Width = 240,
                PlaceholderText = "Filter assets..."
            };

            this.assetSearchPanel.Controls.Add(assetSearchTextBox);

            this.jumpToAssetButton = new Button
            {
                Text = "Jump to Selected",
                Dock = DockStyle.Bottom,
                Height = 30
            };

            // Asset ListView
            this.assetListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            this.assetTypeColumn = new ColumnHeader { Text = "Type", Width = 90 };
            this.assetNameColumn = new ColumnHeader { Text = "Name", Width = 140 };
            this.assetStartColumn = new ColumnHeader { Text = "Start", Width = 70 };
            this.assetEndColumn = new ColumnHeader { Text = "End", Width = 70 };
            this.assetListView.Columns.AddRange(new ColumnHeader[] {
                assetTypeColumn,
                assetNameColumn,
                assetStartColumn,
                assetEndColumn
            });

            // Assemble asset panel (order matters for docking - add in reverse order)
            this.assetPanel.Controls.Add(assetListView);
            this.assetPanel.Controls.Add(assetSearchPanel);
            this.assetPanel.Controls.Add(assetPanelLabel);
            this.assetPanel.Controls.Add(jumpToAssetButton);

            // Main SplitContainer
            this.mainSplitContainer = new SplitContainer();
            this.mainSplitContainer.Dock = DockStyle.Fill;
            this.mainSplitContainer.Orientation = Orientation.Vertical;

            // Panel 1 - Hex view with inspector
            var hexPanel = new Panel { Dock = DockStyle.Fill };
            hexPanel.Controls.Add(hexBox);
            hexPanel.Controls.Add(inspectorTextBox);
            this.mainSplitContainer.Panel1.Controls.Add(hexPanel);

            // Panel 2 - Asset panel
            this.mainSplitContainer.Panel2.Controls.Add(assetPanel);

            // Form layout
            this.MainMenuStrip = this.menuStrip1;
            this.Controls.Add(mainSplitContainer);
            this.Controls.Add(statusStrip1);
            this.Controls.Add(menuStrip1);

            this.ClientSize = new Size(1000, 700);
            this.Text = "Zone File Hex Viewer";
            this.StartPosition = FormStartPosition.CenterParent;

            // Set min sizes after form is sized
            this.mainSplitContainer.Panel1MinSize = 100;
            this.mainSplitContainer.Panel2MinSize = 100;
        }
    }
}
