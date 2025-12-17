namespace Call_of_Duty_FastFile_Editor.UI
{
    partial class AssetSelectionDialog
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
            this.assetListView = new System.Windows.Forms.ListView();
            this.typeColumn = new System.Windows.Forms.ColumnHeader();
            this.countColumn = new System.Windows.Forms.ColumnHeader();
            this.supportedColumn = new System.Windows.Forms.ColumnHeader();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.titleLabel = new System.Windows.Forms.Label();
            this.summaryLabel = new System.Windows.Forms.Label();
            this.selectAllButton = new System.Windows.Forms.Button();
            this.selectNoneButton = new System.Windows.Forms.Button();
            this.infoLabel = new System.Windows.Forms.Label();
            this.fileInfoGroupBox = new System.Windows.Forms.GroupBox();
            this.fileInfoTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.gameLabel = new System.Windows.Forms.Label();
            this.platformLabel = new System.Windows.Forms.Label();
            this.signedLabel = new System.Windows.Forms.Label();
            this.sizeLabel = new System.Windows.Forms.Label();
            this.fileInfoGroupBox.SuspendLayout();
            this.SuspendLayout();
            //
            // fileInfoGroupBox
            //
            this.fileInfoGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fileInfoGroupBox.Controls.Add(this.fileInfoTableLayout);
            this.fileInfoGroupBox.Location = new System.Drawing.Point(12, 12);
            this.fileInfoGroupBox.Name = "fileInfoGroupBox";
            this.fileInfoGroupBox.Padding = new System.Windows.Forms.Padding(8, 3, 8, 3);
            this.fileInfoGroupBox.Size = new System.Drawing.Size(476, 50);
            this.fileInfoGroupBox.TabIndex = 8;
            this.fileInfoGroupBox.TabStop = false;
            this.fileInfoGroupBox.Text = "Source File Information";
            //
            // fileInfoTableLayout
            //
            this.fileInfoTableLayout.ColumnCount = 4;
            this.fileInfoTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.fileInfoTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.fileInfoTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.fileInfoTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.fileInfoTableLayout.Controls.Add(this.gameLabel, 0, 0);
            this.fileInfoTableLayout.Controls.Add(this.platformLabel, 1, 0);
            this.fileInfoTableLayout.Controls.Add(this.signedLabel, 2, 0);
            this.fileInfoTableLayout.Controls.Add(this.sizeLabel, 3, 0);
            this.fileInfoTableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileInfoTableLayout.Location = new System.Drawing.Point(8, 19);
            this.fileInfoTableLayout.Name = "fileInfoTableLayout";
            this.fileInfoTableLayout.RowCount = 1;
            this.fileInfoTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.fileInfoTableLayout.Size = new System.Drawing.Size(460, 28);
            this.fileInfoTableLayout.TabIndex = 0;
            //
            // gameLabel
            //
            this.gameLabel.AutoSize = true;
            this.gameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gameLabel.Location = new System.Drawing.Point(3, 0);
            this.gameLabel.Name = "gameLabel";
            this.gameLabel.Padding = new System.Windows.Forms.Padding(0, 0, 15, 0);
            this.gameLabel.Size = new System.Drawing.Size(92, 28);
            this.gameLabel.TabIndex = 0;
            this.gameLabel.Text = "Game: -";
            this.gameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // platformLabel
            //
            this.platformLabel.AutoSize = true;
            this.platformLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.platformLabel.Location = new System.Drawing.Point(101, 0);
            this.platformLabel.Name = "platformLabel";
            this.platformLabel.Padding = new System.Windows.Forms.Padding(0, 0, 15, 0);
            this.platformLabel.Size = new System.Drawing.Size(140, 28);
            this.platformLabel.TabIndex = 1;
            this.platformLabel.Text = "Platform: -";
            this.platformLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // signedLabel
            //
            this.signedLabel.AutoSize = true;
            this.signedLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.signedLabel.Location = new System.Drawing.Point(199, 0);
            this.signedLabel.Name = "signedLabel";
            this.signedLabel.Padding = new System.Windows.Forms.Padding(0, 0, 15, 0);
            this.signedLabel.Size = new System.Drawing.Size(92, 28);
            this.signedLabel.TabIndex = 2;
            this.signedLabel.Text = "Signed: -";
            this.signedLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // sizeLabel
            //
            this.sizeLabel.AutoSize = true;
            this.sizeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sizeLabel.Location = new System.Drawing.Point(297, 0);
            this.sizeLabel.Name = "sizeLabel";
            this.sizeLabel.Size = new System.Drawing.Size(94, 28);
            this.sizeLabel.TabIndex = 3;
            this.sizeLabel.Text = "Size: -";
            this.sizeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // assetListView
            //
            this.assetListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.assetListView.CheckBoxes = true;
            this.assetListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.typeColumn,
            this.countColumn,
            this.supportedColumn});
            this.assetListView.FullRowSelect = true;
            this.assetListView.GridLines = true;
            this.assetListView.Location = new System.Drawing.Point(12, 130);
            this.assetListView.Name = "assetListView";
            this.assetListView.Size = new System.Drawing.Size(476, 250);
            this.assetListView.TabIndex = 0;
            this.assetListView.UseCompatibleStateImageBehavior = false;
            this.assetListView.View = System.Windows.Forms.View.Details;
            this.assetListView.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.assetListView_ItemCheck);
            //
            // typeColumn
            //
            this.typeColumn.Text = "Asset Type";
            this.typeColumn.Width = 180;
            //
            // countColumn
            //
            this.countColumn.Text = "Count";
            this.countColumn.Width = 80;
            //
            // supportedColumn
            //
            this.supportedColumn.Text = "Supported";
            this.supportedColumn.Width = 80;
            //
            // okButton
            //
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.Location = new System.Drawing.Point(332, 430);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 28);
            this.okButton.TabIndex = 1;
            this.okButton.Text = "Load";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            //
            // cancelButton
            //
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(413, 430);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 28);
            this.cancelButton.TabIndex = 2;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            //
            // titleLabel
            //
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.titleLabel.Location = new System.Drawing.Point(12, 72);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(230, 20);
            this.titleLabel.TabIndex = 3;
            this.titleLabel.Text = "Select Asset Types to Load";
            //
            // summaryLabel
            //
            this.summaryLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.summaryLabel.AutoSize = true;
            this.summaryLabel.Location = new System.Drawing.Point(12, 388);
            this.summaryLabel.Name = "summaryLabel";
            this.summaryLabel.Size = new System.Drawing.Size(150, 15);
            this.summaryLabel.TabIndex = 4;
            this.summaryLabel.Text = "Total: 0 assets";
            //
            // selectAllButton
            //
            this.selectAllButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectAllButton.Location = new System.Drawing.Point(12, 430);
            this.selectAllButton.Name = "selectAllButton";
            this.selectAllButton.Size = new System.Drawing.Size(90, 28);
            this.selectAllButton.TabIndex = 5;
            this.selectAllButton.Text = "Select All";
            this.selectAllButton.UseVisualStyleBackColor = true;
            this.selectAllButton.Click += new System.EventHandler(this.selectAllButton_Click);
            //
            // selectNoneButton
            //
            this.selectNoneButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.selectNoneButton.Location = new System.Drawing.Point(108, 430);
            this.selectNoneButton.Name = "selectNoneButton";
            this.selectNoneButton.Size = new System.Drawing.Size(90, 28);
            this.selectNoneButton.TabIndex = 6;
            this.selectNoneButton.Text = "Select None";
            this.selectNoneButton.UseVisualStyleBackColor = true;
            this.selectNoneButton.Click += new System.EventHandler(this.selectNoneButton_Click);
            //
            // infoLabel
            //
            this.infoLabel.AutoSize = true;
            this.infoLabel.ForeColor = System.Drawing.Color.DimGray;
            this.infoLabel.Location = new System.Drawing.Point(12, 100);
            this.infoLabel.Name = "infoLabel";
            this.infoLabel.Size = new System.Drawing.Size(380, 15);
            this.infoLabel.TabIndex = 7;
            this.infoLabel.Text = "Check the asset types you want to load. Gray items are not yet supported.";
            //
            // AssetSelectionDialog
            //
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(500, 471);
            this.Controls.Add(this.fileInfoGroupBox);
            this.Controls.Add(this.infoLabel);
            this.Controls.Add(this.selectNoneButton);
            this.Controls.Add(this.selectAllButton);
            this.Controls.Add(this.summaryLabel);
            this.Controls.Add(this.titleLabel);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.assetListView);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AssetSelectionDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Asset Selection";
            this.fileInfoGroupBox.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ListView assetListView;
        private System.Windows.Forms.ColumnHeader typeColumn;
        private System.Windows.Forms.ColumnHeader countColumn;
        private System.Windows.Forms.ColumnHeader supportedColumn;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label summaryLabel;
        private System.Windows.Forms.Button selectAllButton;
        private System.Windows.Forms.Button selectNoneButton;
        private System.Windows.Forms.Label infoLabel;
        private System.Windows.Forms.GroupBox fileInfoGroupBox;
        private System.Windows.Forms.TableLayoutPanel fileInfoTableLayout;
        private System.Windows.Forms.Label gameLabel;
        private System.Windows.Forms.Label platformLabel;
        private System.Windows.Forms.Label signedLabel;
        private System.Windows.Forms.Label sizeLabel;
    }
}
