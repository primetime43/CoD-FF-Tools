namespace FastFileCompilerGUI;

partial class MainForm
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
        this.components = new System.ComponentModel.Container();

        // Main layout
        this.mainTableLayout = new TableLayoutPanel();
        this.topPanel = new Panel();
        this.fileListView = new ListView();
        this.columnName = new ColumnHeader();
        this.columnSize = new ColumnHeader();
        this.columnPath = new ColumnHeader();
        this.buttonPanel = new FlowLayoutPanel();
        this.bottomPanel = new Panel();

        // Buttons
        this.btnLoadExistingFF = new Button();
        this.btnAddFiles = new Button();
        this.btnAddFolder = new Button();
        this.btnRemove = new Button();
        this.btnClear = new Button();
        this.btnMoveUp = new Button();
        this.btnMoveDown = new Button();

        // Options
        this.groupBoxOptions = new GroupBox();
        this.labelGame = new Label();
        this.comboBoxGame = new ComboBox();
        this.labelZoneName = new Label();
        this.textBoxZoneName = new TextBox();
        this.checkBoxSaveZone = new CheckBox();
        this.checkBoxIncludeExisting = new CheckBox();
        this.labelLoadedFF = new Label();

        // Compile
        this.groupBoxCompile = new GroupBox();
        this.btnCompile = new Button();
        this.progressBar = new ProgressBar();
        this.labelStatus = new Label();

        // Context menu
        this.contextMenuStrip = new ContextMenuStrip(this.components);
        this.menuItemRename = new ToolStripMenuItem();
        this.menuItemRemove = new ToolStripMenuItem();

        // Menu strip
        this.menuStrip = new MenuStrip();
        this.fileToolStripMenuItem = new ToolStripMenuItem();
        this.newProjectMenuItem = new ToolStripMenuItem();
        this.loadExistingFFMenuItem = new ToolStripMenuItem();
        this.toolStripSeparator1 = new ToolStripSeparator();
        this.exitMenuItem = new ToolStripMenuItem();
        this.helpToolStripMenuItem = new ToolStripMenuItem();
        this.aboutMenuItem = new ToolStripMenuItem();

        this.mainTableLayout.SuspendLayout();
        this.topPanel.SuspendLayout();
        this.buttonPanel.SuspendLayout();
        this.bottomPanel.SuspendLayout();
        this.groupBoxOptions.SuspendLayout();
        this.groupBoxCompile.SuspendLayout();
        this.contextMenuStrip.SuspendLayout();
        this.menuStrip.SuspendLayout();
        this.SuspendLayout();

        //
        // menuStrip
        //
        this.menuStrip.Items.AddRange(new ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.helpToolStripMenuItem
        });
        this.menuStrip.Location = new Point(0, 0);
        this.menuStrip.Name = "menuStrip";
        this.menuStrip.Size = new Size(800, 24);
        this.menuStrip.TabIndex = 0;

        //
        // fileToolStripMenuItem
        //
        this.fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
            this.newProjectMenuItem,
            this.loadExistingFFMenuItem,
            this.toolStripSeparator1,
            this.exitMenuItem
        });
        this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        this.fileToolStripMenuItem.Text = "&File";

        //
        // newProjectMenuItem
        //
        this.newProjectMenuItem.Name = "newProjectMenuItem";
        this.newProjectMenuItem.Text = "&New Project";
        this.newProjectMenuItem.ShortcutKeys = Keys.Control | Keys.N;
        this.newProjectMenuItem.Click += new EventHandler(this.btnClear_Click);

        //
        // loadExistingFFMenuItem
        //
        this.loadExistingFFMenuItem.Name = "loadExistingFFMenuItem";
        this.loadExistingFFMenuItem.Text = "&Load Existing FastFile...";
        this.loadExistingFFMenuItem.ShortcutKeys = Keys.Control | Keys.O;
        this.loadExistingFFMenuItem.Click += new EventHandler(this.btnLoadExistingFF_Click);

        //
        // exitMenuItem
        //
        this.exitMenuItem.Name = "exitMenuItem";
        this.exitMenuItem.Text = "E&xit";
        this.exitMenuItem.Click += new EventHandler(this.exitMenuItem_Click);

        //
        // helpToolStripMenuItem
        //
        this.helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
            this.aboutMenuItem
        });
        this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
        this.helpToolStripMenuItem.Text = "&Help";

        //
        // aboutMenuItem
        //
        this.aboutMenuItem.Name = "aboutMenuItem";
        this.aboutMenuItem.Text = "&About";
        this.aboutMenuItem.Click += new EventHandler(this.aboutMenuItem_Click);

        //
        // mainTableLayout
        //
        this.mainTableLayout.ColumnCount = 1;
        this.mainTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.mainTableLayout.Controls.Add(this.topPanel, 0, 0);
        this.mainTableLayout.Controls.Add(this.fileListView, 0, 1);
        this.mainTableLayout.Controls.Add(this.buttonPanel, 0, 2);
        this.mainTableLayout.Controls.Add(this.bottomPanel, 0, 3);
        this.mainTableLayout.Dock = DockStyle.Fill;
        this.mainTableLayout.Location = new Point(0, 24);
        this.mainTableLayout.Name = "mainTableLayout";
        this.mainTableLayout.Padding = new Padding(10);
        this.mainTableLayout.RowCount = 4;
        this.mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
        this.mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this.mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        this.mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
        this.mainTableLayout.Size = new Size(900, 526);
        this.mainTableLayout.TabIndex = 1;

        //
        // topPanel
        //
        this.topPanel.Dock = DockStyle.Fill;
        this.topPanel.Name = "topPanel";

        //
        // fileListView
        //
        this.fileListView.AllowDrop = true;
        this.fileListView.Columns.AddRange(new ColumnHeader[] {
            this.columnName,
            this.columnSize,
            this.columnPath
        });
        this.fileListView.ContextMenuStrip = this.contextMenuStrip;
        this.fileListView.Dock = DockStyle.Fill;
        this.fileListView.FullRowSelect = true;
        this.fileListView.GridLines = true;
        this.fileListView.Name = "fileListView";
        this.fileListView.View = View.Details;
        this.fileListView.DragEnter += new DragEventHandler(this.fileListView_DragEnter);
        this.fileListView.DragDrop += new DragEventHandler(this.fileListView_DragDrop);

        //
        // columnName
        //
        this.columnName.Text = "Asset Name";
        this.columnName.Width = 300;

        //
        // columnSize
        //
        this.columnSize.Text = "Size";
        this.columnSize.Width = 80;

        //
        // columnPath
        //
        this.columnPath.Text = "Source Path";
        this.columnPath.Width = 380;

        //
        // buttonPanel
        //
        this.buttonPanel.Controls.Add(this.btnLoadExistingFF);
        this.buttonPanel.Controls.Add(this.btnAddFiles);
        this.buttonPanel.Controls.Add(this.btnAddFolder);
        this.buttonPanel.Controls.Add(this.btnRemove);
        this.buttonPanel.Controls.Add(this.btnClear);
        this.buttonPanel.Controls.Add(this.btnMoveUp);
        this.buttonPanel.Controls.Add(this.btnMoveDown);
        this.buttonPanel.Controls.Add(this.btnCompile);
        this.buttonPanel.Dock = DockStyle.Fill;
        this.buttonPanel.Name = "buttonPanel";
        this.buttonPanel.WrapContents = true;
        this.buttonPanel.AutoSize = true;

        //
        // btnLoadExistingFF
        //
        this.btnLoadExistingFF.Name = "btnLoadExistingFF";
        this.btnLoadExistingFF.Size = new Size(85, 30);
        this.btnLoadExistingFF.Text = "Load FF...";
        this.btnLoadExistingFF.UseVisualStyleBackColor = true;
        this.btnLoadExistingFF.Click += new EventHandler(this.btnLoadExistingFF_Click);

        //
        // btnAddFiles
        //
        this.btnAddFiles.Name = "btnAddFiles";
        this.btnAddFiles.Size = new Size(85, 30);
        this.btnAddFiles.Text = "Add Files...";
        this.btnAddFiles.UseVisualStyleBackColor = true;
        this.btnAddFiles.Click += new EventHandler(this.btnAddFiles_Click);

        //
        // btnAddFolder
        //
        this.btnAddFolder.Name = "btnAddFolder";
        this.btnAddFolder.Size = new Size(90, 30);
        this.btnAddFolder.Text = "Add Folder...";
        this.btnAddFolder.UseVisualStyleBackColor = true;
        this.btnAddFolder.Click += new EventHandler(this.btnAddFolder_Click);

        //
        // btnRemove
        //
        this.btnRemove.Name = "btnRemove";
        this.btnRemove.Size = new Size(75, 30);
        this.btnRemove.Text = "Remove";
        this.btnRemove.UseVisualStyleBackColor = true;
        this.btnRemove.Click += new EventHandler(this.btnRemove_Click);

        //
        // btnClear
        //
        this.btnClear.Name = "btnClear";
        this.btnClear.Size = new Size(75, 30);
        this.btnClear.Text = "Clear All";
        this.btnClear.UseVisualStyleBackColor = true;
        this.btnClear.Click += new EventHandler(this.btnClear_Click);

        //
        // btnMoveUp
        //
        this.btnMoveUp.Name = "btnMoveUp";
        this.btnMoveUp.Size = new Size(75, 30);
        this.btnMoveUp.Text = "Move Up";
        this.btnMoveUp.UseVisualStyleBackColor = true;
        this.btnMoveUp.Click += new EventHandler(this.btnMoveUp_Click);

        //
        // btnMoveDown
        //
        this.btnMoveDown.Name = "btnMoveDown";
        this.btnMoveDown.Size = new Size(85, 30);
        this.btnMoveDown.Text = "Move Down";
        this.btnMoveDown.UseVisualStyleBackColor = true;
        this.btnMoveDown.Click += new EventHandler(this.btnMoveDown_Click);

        //
        // bottomPanel
        //
        this.bottomPanel.Controls.Add(this.groupBoxOptions);
        this.bottomPanel.Controls.Add(this.groupBoxCompile);
        this.bottomPanel.Dock = DockStyle.Fill;
        this.bottomPanel.Name = "bottomPanel";

        //
        // groupBoxOptions
        //
        this.groupBoxOptions.Controls.Add(this.labelGame);
        this.groupBoxOptions.Controls.Add(this.comboBoxGame);
        this.groupBoxOptions.Controls.Add(this.labelZoneName);
        this.groupBoxOptions.Controls.Add(this.textBoxZoneName);
        this.groupBoxOptions.Controls.Add(this.checkBoxSaveZone);
        this.groupBoxOptions.Controls.Add(this.checkBoxIncludeExisting);
        this.groupBoxOptions.Controls.Add(this.labelLoadedFF);
        this.groupBoxOptions.Location = new Point(0, 5);
        this.groupBoxOptions.Name = "groupBoxOptions";
        this.groupBoxOptions.Size = new Size(400, 145);
        this.groupBoxOptions.TabIndex = 0;
        this.groupBoxOptions.TabStop = false;
        this.groupBoxOptions.Text = "Options";

        //
        // labelGame
        //
        this.labelGame.AutoSize = true;
        this.labelGame.Location = new Point(15, 30);
        this.labelGame.Name = "labelGame";
        this.labelGame.Text = "Game Version:";

        //
        // comboBoxGame
        //
        this.comboBoxGame.DropDownStyle = ComboBoxStyle.DropDownList;
        this.comboBoxGame.FormattingEnabled = true;
        this.comboBoxGame.Items.AddRange(new object[] {
            "Call of Duty 4: Modern Warfare",
            "Call of Duty: World at War",
            "Call of Duty: Modern Warfare 2"
        });
        this.comboBoxGame.Location = new Point(120, 27);
        this.comboBoxGame.Name = "comboBoxGame";
        this.comboBoxGame.Size = new Size(250, 23);
        this.comboBoxGame.SelectedIndex = 0;

        //
        // labelZoneName
        //
        this.labelZoneName.AutoSize = true;
        this.labelZoneName.Location = new Point(15, 65);
        this.labelZoneName.Name = "labelZoneName";
        this.labelZoneName.Text = "Zone Name:";

        //
        // textBoxZoneName
        //
        this.textBoxZoneName.Location = new Point(120, 62);
        this.textBoxZoneName.Name = "textBoxZoneName";
        this.textBoxZoneName.Size = new Size(250, 23);
        this.textBoxZoneName.Text = "custom_patch_mp";

        //
        // checkBoxSaveZone
        //
        this.checkBoxSaveZone.AutoSize = true;
        this.checkBoxSaveZone.Location = new Point(18, 95);
        this.checkBoxSaveZone.Name = "checkBoxSaveZone";
        this.checkBoxSaveZone.Size = new Size(220, 19);
        this.checkBoxSaveZone.Text = "Also save uncompressed .zone file";
        this.checkBoxSaveZone.UseVisualStyleBackColor = true;

        //
        // checkBoxIncludeExisting
        //
        this.checkBoxIncludeExisting.AutoSize = true;
        this.checkBoxIncludeExisting.Checked = true;
        this.checkBoxIncludeExisting.CheckState = CheckState.Checked;
        this.checkBoxIncludeExisting.Enabled = false;
        this.checkBoxIncludeExisting.Location = new Point(18, 115);
        this.checkBoxIncludeExisting.Name = "checkBoxIncludeExisting";
        this.checkBoxIncludeExisting.Size = new Size(220, 19);
        this.checkBoxIncludeExisting.Text = "Include existing assets from loaded FF";
        this.checkBoxIncludeExisting.UseVisualStyleBackColor = true;

        //
        // labelLoadedFF
        //
        this.labelLoadedFF.AutoSize = true;
        this.labelLoadedFF.ForeColor = System.Drawing.Color.Gray;
        this.labelLoadedFF.Location = new Point(240, 116);
        this.labelLoadedFF.Name = "labelLoadedFF";
        this.labelLoadedFF.Text = "(No FF loaded)";

        //
        // groupBoxCompile
        //
        this.groupBoxCompile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.groupBoxCompile.Controls.Add(this.progressBar);
        this.groupBoxCompile.Controls.Add(this.labelStatus);
        this.groupBoxCompile.Location = new Point(410, 5);
        this.groupBoxCompile.Name = "groupBoxCompile";
        this.groupBoxCompile.Size = new Size(450, 145);
        this.groupBoxCompile.TabIndex = 1;
        this.groupBoxCompile.TabStop = false;
        this.groupBoxCompile.Text = "Status";

        //
        // btnCompile
        //
        this.btnCompile.Name = "btnCompile";
        this.btnCompile.Size = new Size(90, 30);
        this.btnCompile.Text = "Create FF...";
        this.btnCompile.UseVisualStyleBackColor = true;
        this.btnCompile.Click += new EventHandler(this.btnCompile_Click);

        //
        // progressBar
        //
        this.progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.progressBar.Location = new Point(15, 35);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new Size(420, 23);

        //
        // labelStatus
        //
        this.labelStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.labelStatus.Location = new Point(15, 70);
        this.labelStatus.Name = "labelStatus";
        this.labelStatus.Size = new Size(420, 60);
        this.labelStatus.Text = "Ready";

        //
        // contextMenuStrip
        //
        this.contextMenuStrip.Items.AddRange(new ToolStripItem[] {
            this.menuItemRename,
            this.menuItemRemove
        });
        this.contextMenuStrip.Name = "contextMenuStrip";
        this.contextMenuStrip.Size = new Size(150, 48);

        //
        // menuItemRename
        //
        this.menuItemRename.Name = "menuItemRename";
        this.menuItemRename.Text = "Rename Asset...";
        this.menuItemRename.Click += new EventHandler(this.menuItemRename_Click);

        //
        // menuItemRemove
        //
        this.menuItemRemove.Name = "menuItemRemove";
        this.menuItemRemove.Text = "Remove";
        this.menuItemRemove.Click += new EventHandler(this.btnRemove_Click);

        //
        // MainForm
        //
        this.AllowDrop = true;
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(900, 550);
        this.Controls.Add(this.mainTableLayout);
        this.Controls.Add(this.menuStrip);
        this.MainMenuStrip = this.menuStrip;
        this.MinimumSize = new Size(850, 500);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "FastFile Compiler - Call of Duty";

        this.mainTableLayout.ResumeLayout(false);
        this.topPanel.ResumeLayout(false);
        this.buttonPanel.ResumeLayout(false);
        this.bottomPanel.ResumeLayout(false);
        this.groupBoxOptions.ResumeLayout(false);
        this.groupBoxOptions.PerformLayout();
        this.groupBoxCompile.ResumeLayout(false);
        this.contextMenuStrip.ResumeLayout(false);
        this.menuStrip.ResumeLayout(false);
        this.menuStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private TableLayoutPanel mainTableLayout;
    private Panel topPanel;
    private ListView fileListView;
    private ColumnHeader columnName;
    private ColumnHeader columnSize;
    private ColumnHeader columnPath;
    private FlowLayoutPanel buttonPanel;
    private Panel bottomPanel;
    private Button btnLoadExistingFF;
    private Button btnAddFiles;
    private Button btnAddFolder;
    private Button btnRemove;
    private Button btnClear;
    private Button btnMoveUp;
    private Button btnMoveDown;
    private GroupBox groupBoxOptions;
    private Label labelGame;
    private ComboBox comboBoxGame;
    private Label labelZoneName;
    private TextBox textBoxZoneName;
    private CheckBox checkBoxSaveZone;
    private CheckBox checkBoxIncludeExisting;
    private Label labelLoadedFF;
    private GroupBox groupBoxCompile;
    private Button btnCompile;
    private ProgressBar progressBar;
    private Label labelStatus;
    private ContextMenuStrip contextMenuStrip;
    private ToolStripMenuItem menuItemRename;
    private ToolStripMenuItem menuItemRemove;
    private MenuStrip menuStrip;
    private ToolStripMenuItem fileToolStripMenuItem;
    private ToolStripMenuItem newProjectMenuItem;
    private ToolStripMenuItem loadExistingFFMenuItem;
    private ToolStripSeparator toolStripSeparator1;
    private ToolStripMenuItem exitMenuItem;
    private ToolStripMenuItem helpToolStripMenuItem;
    private ToolStripMenuItem aboutMenuItem;
}
