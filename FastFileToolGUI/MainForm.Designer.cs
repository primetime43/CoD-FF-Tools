namespace FastFileToolGUI;

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
        tabControl = new TabControl();
        extractTabPage = new TabPage();
        extractGroupBox = new GroupBox();
        detailsTextBox = new TextBox();
        fileInfoLabel = new Label();
        extractButton = new Button();
        extractOutputBrowseButton = new Button();
        extractOutputTextBox = new TextBox();
        extractOutputLabel = new Label();
        extractBrowseButton = new Button();
        extractInputTextBox = new TextBox();
        extractInputLabel = new Label();
        packTabPage = new TabPage();
        packGroupBox = new GroupBox();
        gameVersionLabel = new Label();
        gameVersionComboBox = new ComboBox();
        packButton = new Button();
        packOutputBrowseButton = new Button();
        packOutputTextBox = new TextBox();
        packOutputLabel = new Label();
        packBrowseButton = new Button();
        packInputTextBox = new TextBox();
        packInputLabel = new Label();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        titleLabel = new Label();
        subtitleLabel = new Label();
        tabControl.SuspendLayout();
        extractTabPage.SuspendLayout();
        extractGroupBox.SuspendLayout();
        packTabPage.SuspendLayout();
        packGroupBox.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        //
        // tabControl
        //
        tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tabControl.Controls.Add(extractTabPage);
        tabControl.Controls.Add(packTabPage);
        tabControl.Location = new Point(12, 55);
        tabControl.Name = "tabControl";
        tabControl.SelectedIndex = 0;
        tabControl.Size = new Size(610, 355);
        tabControl.TabIndex = 0;
        //
        // extractTabPage
        //
        extractTabPage.Controls.Add(extractGroupBox);
        extractTabPage.Location = new Point(4, 24);
        extractTabPage.Name = "extractTabPage";
        extractTabPage.Padding = new Padding(3);
        extractTabPage.Size = new Size(602, 327);
        extractTabPage.TabIndex = 0;
        extractTabPage.Text = "Extract (FF → Zone)";
        extractTabPage.UseVisualStyleBackColor = true;
        //
        // extractGroupBox
        //
        extractGroupBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        extractGroupBox.Controls.Add(detailsTextBox);
        extractGroupBox.Controls.Add(fileInfoLabel);
        extractGroupBox.Controls.Add(extractButton);
        extractGroupBox.Controls.Add(extractOutputBrowseButton);
        extractGroupBox.Controls.Add(extractOutputTextBox);
        extractGroupBox.Controls.Add(extractOutputLabel);
        extractGroupBox.Controls.Add(extractBrowseButton);
        extractGroupBox.Controls.Add(extractInputTextBox);
        extractGroupBox.Controls.Add(extractInputLabel);
        extractGroupBox.Location = new Point(6, 6);
        extractGroupBox.Name = "extractGroupBox";
        extractGroupBox.Size = new Size(590, 310);
        extractGroupBox.TabIndex = 0;
        extractGroupBox.TabStop = false;
        extractGroupBox.Text = "Extract Zone from FastFile";
        //
        // detailsTextBox
        //
        detailsTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        detailsTextBox.BackColor = SystemColors.Info;
        detailsTextBox.Font = new Font("Consolas", 9F);
        detailsTextBox.Location = new Point(15, 170);
        detailsTextBox.Multiline = true;
        detailsTextBox.Name = "detailsTextBox";
        detailsTextBox.ReadOnly = true;
        detailsTextBox.ScrollBars = ScrollBars.Vertical;
        detailsTextBox.Size = new Size(555, 130);
        detailsTextBox.TabIndex = 8;
        detailsTextBox.Text = "Select a FastFile to see detailed information...";
        //
        // fileInfoLabel
        //
        fileInfoLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        fileInfoLabel.Font = new Font("Segoe UI", 8F);
        fileInfoLabel.ForeColor = Color.DimGray;
        fileInfoLabel.Location = new Point(15, 73);
        fileInfoLabel.Name = "fileInfoLabel";
        fileInfoLabel.Size = new Size(555, 15);
        fileInfoLabel.TabIndex = 7;
        fileInfoLabel.Text = "Select a FastFile to see info";
        //
        // extractButton
        //
        extractButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        extractButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        extractButton.Location = new Point(470, 140);
        extractButton.Name = "extractButton";
        extractButton.Size = new Size(100, 30);
        extractButton.TabIndex = 6;
        extractButton.Text = "Extract";
        extractButton.UseVisualStyleBackColor = true;
        extractButton.Click += extractButton_Click;
        //
        // extractOutputBrowseButton
        //
        extractOutputBrowseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        extractOutputBrowseButton.Location = new Point(520, 111);
        extractOutputBrowseButton.Name = "extractOutputBrowseButton";
        extractOutputBrowseButton.Size = new Size(50, 23);
        extractOutputBrowseButton.TabIndex = 5;
        extractOutputBrowseButton.Text = "...";
        extractOutputBrowseButton.UseVisualStyleBackColor = true;
        extractOutputBrowseButton.Click += extractOutputBrowseButton_Click;
        //
        // extractOutputTextBox
        //
        extractOutputTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        extractOutputTextBox.Location = new Point(15, 111);
        extractOutputTextBox.Name = "extractOutputTextBox";
        extractOutputTextBox.Size = new Size(499, 23);
        extractOutputTextBox.TabIndex = 4;
        //
        // extractOutputLabel
        //
        extractOutputLabel.AutoSize = true;
        extractOutputLabel.Location = new Point(15, 93);
        extractOutputLabel.Name = "extractOutputLabel";
        extractOutputLabel.Size = new Size(100, 15);
        extractOutputLabel.TabIndex = 3;
        extractOutputLabel.Text = "Output Zone File:";
        //
        // extractBrowseButton
        //
        extractBrowseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        extractBrowseButton.Location = new Point(520, 47);
        extractBrowseButton.Name = "extractBrowseButton";
        extractBrowseButton.Size = new Size(50, 23);
        extractBrowseButton.TabIndex = 2;
        extractBrowseButton.Text = "...";
        extractBrowseButton.UseVisualStyleBackColor = true;
        extractBrowseButton.Click += extractBrowseButton_Click;
        //
        // extractInputTextBox
        //
        extractInputTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        extractInputTextBox.Location = new Point(15, 47);
        extractInputTextBox.Name = "extractInputTextBox";
        extractInputTextBox.Size = new Size(499, 23);
        extractInputTextBox.TabIndex = 1;
        //
        // extractInputLabel
        //
        extractInputLabel.AutoSize = true;
        extractInputLabel.Location = new Point(15, 29);
        extractInputLabel.Name = "extractInputLabel";
        extractInputLabel.Size = new Size(96, 15);
        extractInputLabel.TabIndex = 0;
        extractInputLabel.Text = "Input FastFile (.ff):";
        //
        // packTabPage
        //
        packTabPage.Controls.Add(packGroupBox);
        packTabPage.Location = new Point(4, 24);
        packTabPage.Name = "packTabPage";
        packTabPage.Padding = new Padding(3);
        packTabPage.Size = new Size(602, 327);
        packTabPage.TabIndex = 1;
        packTabPage.Text = "Pack (Zone → FF)";
        packTabPage.UseVisualStyleBackColor = true;
        //
        // packGroupBox
        //
        packGroupBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        packGroupBox.Controls.Add(gameVersionLabel);
        packGroupBox.Controls.Add(gameVersionComboBox);
        packGroupBox.Controls.Add(packButton);
        packGroupBox.Controls.Add(packOutputBrowseButton);
        packGroupBox.Controls.Add(packOutputTextBox);
        packGroupBox.Controls.Add(packOutputLabel);
        packGroupBox.Controls.Add(packBrowseButton);
        packGroupBox.Controls.Add(packInputTextBox);
        packGroupBox.Controls.Add(packInputLabel);
        packGroupBox.Location = new Point(6, 6);
        packGroupBox.Name = "packGroupBox";
        packGroupBox.Size = new Size(590, 200);
        packGroupBox.TabIndex = 0;
        packGroupBox.TabStop = false;
        packGroupBox.Text = "Pack Zone into FastFile";
        //
        // gameVersionLabel
        //
        gameVersionLabel.AutoSize = true;
        gameVersionLabel.Location = new Point(15, 130);
        gameVersionLabel.Name = "gameVersionLabel";
        gameVersionLabel.Size = new Size(123, 15);
        gameVersionLabel.TabIndex = 8;
        gameVersionLabel.Text = "Game / Platform:";
        //
        // gameVersionComboBox
        //
        gameVersionComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        gameVersionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        gameVersionComboBox.FormattingEnabled = true;
        gameVersionComboBox.Location = new Point(15, 148);
        gameVersionComboBox.Name = "gameVersionComboBox";
        gameVersionComboBox.Size = new Size(340, 23);
        gameVersionComboBox.TabIndex = 7;
        //
        // packButton
        //
        packButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        packButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        packButton.Location = new Point(470, 160);
        packButton.Name = "packButton";
        packButton.Size = new Size(100, 30);
        packButton.TabIndex = 6;
        packButton.Text = "Pack";
        packButton.UseVisualStyleBackColor = true;
        packButton.Click += packButton_Click;
        //
        // packOutputBrowseButton
        //
        packOutputBrowseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        packOutputBrowseButton.Location = new Point(520, 95);
        packOutputBrowseButton.Name = "packOutputBrowseButton";
        packOutputBrowseButton.Size = new Size(50, 23);
        packOutputBrowseButton.TabIndex = 5;
        packOutputBrowseButton.Text = "...";
        packOutputBrowseButton.UseVisualStyleBackColor = true;
        packOutputBrowseButton.Click += packOutputBrowseButton_Click;
        //
        // packOutputTextBox
        //
        packOutputTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        packOutputTextBox.Location = new Point(15, 95);
        packOutputTextBox.Name = "packOutputTextBox";
        packOutputTextBox.Size = new Size(499, 23);
        packOutputTextBox.TabIndex = 4;
        //
        // packOutputLabel
        //
        packOutputLabel.AutoSize = true;
        packOutputLabel.Location = new Point(15, 77);
        packOutputLabel.Name = "packOutputLabel";
        packOutputLabel.Size = new Size(107, 15);
        packOutputLabel.TabIndex = 3;
        packOutputLabel.Text = "Output FastFile (.ff):";
        //
        // packBrowseButton
        //
        packBrowseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        packBrowseButton.Location = new Point(520, 47);
        packBrowseButton.Name = "packBrowseButton";
        packBrowseButton.Size = new Size(50, 23);
        packBrowseButton.TabIndex = 2;
        packBrowseButton.Text = "...";
        packBrowseButton.UseVisualStyleBackColor = true;
        packBrowseButton.Click += packBrowseButton_Click;
        //
        // packInputTextBox
        //
        packInputTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        packInputTextBox.Location = new Point(15, 47);
        packInputTextBox.Name = "packInputTextBox";
        packInputTextBox.Size = new Size(499, 23);
        packInputTextBox.TabIndex = 1;
        //
        // packInputLabel
        //
        packInputLabel.AutoSize = true;
        packInputLabel.Location = new Point(15, 29);
        packInputLabel.Name = "packInputLabel";
        packInputLabel.Size = new Size(121, 15);
        packInputLabel.TabIndex = 0;
        packInputLabel.Text = "Input Zone File (.zone):";
        //
        // statusStrip
        //
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
        statusStrip.Location = new Point(0, 418);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(634, 22);
        statusStrip.TabIndex = 1;
        //
        // statusLabel
        //
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(39, 17);
        statusLabel.Text = "Ready";
        //
        // titleLabel
        //
        titleLabel.AutoSize = true;
        titleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point);
        titleLabel.Location = new Point(12, 9);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(295, 25);
        titleLabel.TabIndex = 2;
        titleLabel.Text = "FastFile Tool - Extract && Pack";
        //
        // subtitleLabel
        //
        subtitleLabel.AutoSize = true;
        subtitleLabel.ForeColor = Color.DimGray;
        subtitleLabel.Location = new Point(14, 34);
        subtitleLabel.Name = "subtitleLabel";
        subtitleLabel.Size = new Size(350, 15);
        subtitleLabel.TabIndex = 3;
        subtitleLabel.Text = "Supports COD4, WAW, MW2, BO1, MW3, BO2 | PS3, Xbox 360, PC, Wii";
        //
        // MainForm
        //
        AllowDrop = true;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(634, 440);
        Controls.Add(subtitleLabel);
        Controls.Add(titleLabel);
        Controls.Add(statusStrip);
        Controls.Add(tabControl);
        MinimumSize = new Size(550, 400);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "FastFile Tool v3.1.0";
        DragDrop += MainForm_DragDrop;
        DragEnter += MainForm_DragEnter;
        tabControl.ResumeLayout(false);
        extractTabPage.ResumeLayout(false);
        extractGroupBox.ResumeLayout(false);
        extractGroupBox.PerformLayout();
        packTabPage.ResumeLayout(false);
        packGroupBox.ResumeLayout(false);
        packGroupBox.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private TabControl tabControl;
    private TabPage extractTabPage;
    private TabPage packTabPage;
    private GroupBox extractGroupBox;
    private Button extractButton;
    private Button extractOutputBrowseButton;
    private TextBox extractOutputTextBox;
    private Label extractOutputLabel;
    private Button extractBrowseButton;
    private TextBox extractInputTextBox;
    private Label extractInputLabel;
    private GroupBox packGroupBox;
    private Button packButton;
    private Button packOutputBrowseButton;
    private TextBox packOutputTextBox;
    private Label packOutputLabel;
    private Button packBrowseButton;
    private TextBox packInputTextBox;
    private Label packInputLabel;
    private Label fileInfoLabel;
    private ComboBox gameVersionComboBox;
    private Label gameVersionLabel;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel statusLabel;
    private Label titleLabel;
    private Label subtitleLabel;
    private TextBox detailsTextBox;
}
