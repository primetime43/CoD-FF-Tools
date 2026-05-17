namespace Call_of_Duty_FastFile_Editor.UI
{
    partial class LogsTabPage
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripLabel filterLabel;
        private System.Windows.Forms.ToolStripComboBox severityFilter;
        private System.Windows.Forms.ToolStripLabel categoryLabel;
        private System.Windows.Forms.ToolStripComboBox categoryFilter;
        private System.Windows.Forms.ToolStripSeparator sep1;
        private System.Windows.Forms.ToolStripButton clearButton;
        private System.Windows.Forms.ToolStripButton exportButton;
        private System.Windows.Forms.ToolStripSeparator sep2;
        private System.Windows.Forms.ToolStripButton autoScrollButton;
        private System.Windows.Forms.ToolStripSeparator sep3;
        private System.Windows.Forms.ToolStripLabel countLabel;

        private System.Windows.Forms.ListView logListView;
        private System.Windows.Forms.ColumnHeader colTime;
        private System.Windows.Forms.ColumnHeader colSeverity;
        private System.Windows.Forms.ColumnHeader colCategory;
        private System.Windows.Forms.ColumnHeader colMessage;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null) components.Dispose();
                // Unsubscribe from the static LogService events when the control is disposed
                // so leftover handlers don't reference a disposed control.
                FastFileLib.Logging.LogService.EntryAdded -= OnEntryAdded;
                FastFileLib.Logging.LogService.Cleared    -= OnCleared;
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.filterLabel = new System.Windows.Forms.ToolStripLabel();
            this.severityFilter = new System.Windows.Forms.ToolStripComboBox();
            this.categoryLabel = new System.Windows.Forms.ToolStripLabel();
            this.categoryFilter = new System.Windows.Forms.ToolStripComboBox();
            this.sep1 = new System.Windows.Forms.ToolStripSeparator();
            this.clearButton = new System.Windows.Forms.ToolStripButton();
            this.exportButton = new System.Windows.Forms.ToolStripButton();
            this.sep2 = new System.Windows.Forms.ToolStripSeparator();
            this.autoScrollButton = new System.Windows.Forms.ToolStripButton();
            this.sep3 = new System.Windows.Forms.ToolStripSeparator();
            this.countLabel = new System.Windows.Forms.ToolStripLabel();

            this.logListView = new System.Windows.Forms.ListView();
            this.colTime = new System.Windows.Forms.ColumnHeader();
            this.colSeverity = new System.Windows.Forms.ColumnHeader();
            this.colCategory = new System.Windows.Forms.ColumnHeader();
            this.colMessage = new System.Windows.Forms.ColumnHeader();

            // ---- toolStrip
            this.toolStrip.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
            {
                this.filterLabel, this.severityFilter,
                this.categoryLabel, this.categoryFilter,
                this.sep1,
                this.clearButton, this.exportButton,
                this.sep2,
                this.autoScrollButton,
                this.sep3,
                this.countLabel
            });

            // ---- filterLabel
            this.filterLabel.Text = "Severity:";

            // ---- severityFilter
            this.severityFilter.AutoSize = false;
            this.severityFilter.Width = 100;
            this.severityFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // ---- categoryLabel
            this.categoryLabel.Text = "Category:";

            // ---- categoryFilter
            this.categoryFilter.AutoSize = false;
            this.categoryFilter.Width = 160;
            this.categoryFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;

            // ---- clearButton
            this.clearButton.Text = "Clear";
            this.clearButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearButton.ToolTipText = "Clear all log entries from memory";

            // ---- exportButton
            this.exportButton.Text = "Export...";
            this.exportButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.exportButton.ToolTipText = "Save all entries to a .log text file";

            // ---- autoScrollButton
            this.autoScrollButton.Text = "Auto-scroll";
            this.autoScrollButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.autoScrollButton.CheckOnClick = true;
            this.autoScrollButton.Checked = true;
            this.autoScrollButton.ToolTipText = "Scroll to the newest entry as it arrives";

            // ---- countLabel
            this.countLabel.Text = "0 entries";
            this.countLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;

            // ---- logListView
            this.logListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logListView.View = System.Windows.Forms.View.Details;
            this.logListView.FullRowSelect = true;
            this.logListView.GridLines = false;
            this.logListView.HideSelection = false;
            this.logListView.MultiSelect = true;
            this.logListView.UseCompatibleStateImageBehavior = false;
            this.logListView.Font = new System.Drawing.Font("Consolas", 9F);
            this.logListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[]
            {
                this.colTime, this.colSeverity, this.colCategory, this.colMessage
            });

            this.colTime.Text = "Time";
            this.colTime.Width = 90;
            this.colSeverity.Text = "Severity";
            this.colSeverity.Width = 70;
            this.colCategory.Text = "Category";
            this.colCategory.Width = 140;
            this.colMessage.Text = "Message";
            this.colMessage.Width = 900;

            // ---- LogsTabPage (UserControl)
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.logListView);
            this.Controls.Add(this.toolStrip);
            this.Name = "LogsTabPage";
            this.Size = new System.Drawing.Size(1200, 600);
        }
    }
}
