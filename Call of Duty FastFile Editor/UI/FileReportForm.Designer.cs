namespace Call_of_Duty_FastFile_Editor.UI
{
    partial class FileReportForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Panel topBarPanel;
        private System.Windows.Forms.Label fileNameLabel;
        private System.Windows.Forms.Label ffSizeLabel;
        private System.Windows.Forms.Label zoneSizeLabel;

        private System.Windows.Forms.SplitContainer outerSplit;   // top = main area, bottom = summary
        private System.Windows.Forms.SplitContainer innerSplit;   // left = tree, right = report
        private System.Windows.Forms.TreeView sectionsTree;
        private System.Windows.Forms.RichTextBox reportRichTextBox;
        private System.Windows.Forms.TextBox summaryTextBox;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.topBarPanel = new System.Windows.Forms.Panel();
            this.fileNameLabel = new System.Windows.Forms.Label();
            this.ffSizeLabel = new System.Windows.Forms.Label();
            this.zoneSizeLabel = new System.Windows.Forms.Label();

            this.outerSplit = new System.Windows.Forms.SplitContainer();
            this.innerSplit = new System.Windows.Forms.SplitContainer();
            this.sectionsTree = new System.Windows.Forms.TreeView();
            this.reportRichTextBox = new System.Windows.Forms.RichTextBox();
            this.summaryTextBox = new System.Windows.Forms.TextBox();

            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();

            // ---- topBarPanel
            this.topBarPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topBarPanel.Height = 32;
            this.topBarPanel.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.topBarPanel.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);

            // ---- fileNameLabel
            this.fileNameLabel.AutoSize = true;
            this.fileNameLabel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.fileNameLabel.Location = new System.Drawing.Point(8, 6);
            this.fileNameLabel.Text = "(no file)";

            // ---- ffSizeLabel
            this.ffSizeLabel.AutoSize = true;
            this.ffSizeLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.ffSizeLabel.Location = new System.Drawing.Point(400, 8);
            this.ffSizeLabel.Text = "FF size: -";
            this.ffSizeLabel.ForeColor = System.Drawing.Color.DimGray;

            // ---- zoneSizeLabel
            this.zoneSizeLabel.AutoSize = true;
            this.zoneSizeLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.zoneSizeLabel.Location = new System.Drawing.Point(620, 8);
            this.zoneSizeLabel.Text = "Zone size: -";
            this.zoneSizeLabel.ForeColor = System.Drawing.Color.Firebrick;

            this.topBarPanel.Controls.Add(this.fileNameLabel);
            this.topBarPanel.Controls.Add(this.ffSizeLabel);
            this.topBarPanel.Controls.Add(this.zoneSizeLabel);

            // ---- outerSplit (Horizontal: top = report area, bottom = summary)
            // Set Size BEFORE SplitterDistance/MinSize so the validator has real dimensions.
            // ClientSize 1200x820 minus top bar (32) and status strip (~22) = 1200 x 766
            this.outerSplit.SuspendLayout();
            this.outerSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.outerSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.outerSplit.Size = new System.Drawing.Size(1200, 766);
            this.outerSplit.Panel1MinSize = 200;
            this.outerSplit.Panel2MinSize = 180;   // guarantee summary stays visible
            this.outerSplit.SplitterDistance = 540;
            this.outerSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;

            // ---- innerSplit (Vertical: left = tree, right = report)
            // outerSplit Panel1 is 1200 wide x 540 tall, so innerSplit fills that.
            this.innerSplit.SuspendLayout();
            this.innerSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.innerSplit.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.innerSplit.Size = new System.Drawing.Size(1200, 540);
            this.innerSplit.Panel1MinSize = 220;   // keep tree wide enough for full labels
            this.innerSplit.Panel2MinSize = 400;
            this.innerSplit.SplitterDistance = 300;
            this.innerSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;

            // ---- sectionsTree
            this.sectionsTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sectionsTree.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.sectionsTree.HideSelection = false;
            this.sectionsTree.ShowLines = true;
            this.sectionsTree.ShowPlusMinus = true;
            this.sectionsTree.ShowRootLines = true;
            this.sectionsTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.SectionsTree_AfterSelect);

            // ---- reportRichTextBox
            this.reportRichTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.reportRichTextBox.ReadOnly = true;
            this.reportRichTextBox.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.reportRichTextBox.BackColor = System.Drawing.Color.White;
            this.reportRichTextBox.WordWrap = false;
            this.reportRichTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.reportRichTextBox.DetectUrls = false;

            // ---- summaryTextBox
            this.summaryTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.summaryTextBox.Multiline = true;
            this.summaryTextBox.ReadOnly = true;
            this.summaryTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.summaryTextBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.summaryTextBox.BackColor = System.Drawing.Color.WhiteSmoke;
            this.summaryTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;

            // ---- statusStrip
            this.statusStrip.Items.Add(this.statusLabel);
            this.statusLabel.Text = "Ready";

            // Wire panels
            this.innerSplit.Panel1.Controls.Add(this.sectionsTree);
            this.innerSplit.Panel2.Controls.Add(this.reportRichTextBox);
            this.innerSplit.ResumeLayout(false);

            this.outerSplit.Panel1.Controls.Add(this.innerSplit);
            this.outerSplit.Panel2.Controls.Add(this.summaryTextBox);
            this.outerSplit.Panel2.Padding = new System.Windows.Forms.Padding(8);
            this.outerSplit.ResumeLayout(false);

            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 820);
            this.Controls.Add(this.outerSplit);
            this.Controls.Add(this.topBarPanel);
            this.Controls.Add(this.statusStrip);
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.Name = "FileReportForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "File Report";
        }
    }
}
