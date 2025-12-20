using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    public partial class SpaceTransferForm : Form
    {
        private List<RawFileNode> _rawFileNodes;
        private ListView _donorListView;
        private ListView _recipientListView;
        private NumericUpDown _bytesToTransferNumeric;
        private Label _donorInfoLabel;
        private Label _recipientInfoLabel;
        private Label _summaryLabel;
        private Button _transferButton;
        private Button _cancelButton;

        public RawFileNode SelectedDonor { get; private set; }
        public RawFileNode SelectedRecipient { get; private set; }
        public int BytesToTransfer { get; private set; }
        public bool UseInPlaceTransfer { get; private set; } = true;

        private RadioButton _inPlaceRadio;
        private RadioButton _rebuildRadio;

        public SpaceTransferForm(List<RawFileNode> rawFileNodes)
        {
            _rawFileNodes = rawFileNodes;
            InitializeComponents();
            PopulateListViews();
        }

        private void InitializeComponents()
        {
            this.Text = "Transfer Space Between Files";
            this.Size = new System.Drawing.Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Instructions
            var instructionLabel = new Label
            {
                Text = "Transfer allocated space from one file to another. Select a donor file (with unused space) and a recipient file.",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(860, 20),
                AutoSize = false
            };
            this.Controls.Add(instructionLabel);

            // Donor section
            var donorGroupBox = new GroupBox
            {
                Text = "Donor File (has unused space)",
                Location = new System.Drawing.Point(12, 40),
                Size = new System.Drawing.Size(420, 300)
            };

            _donorListView = new ListView
            {
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(400, 230),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true
            };
            _donorListView.Columns.Add("File Name", 200);
            _donorListView.Columns.Add("Used", 70);
            _donorListView.Columns.Add("Allocated", 70);
            _donorListView.Columns.Add("Free", 50);
            _donorListView.SelectedIndexChanged += DonorListView_SelectedIndexChanged;
            donorGroupBox.Controls.Add(_donorListView);

            _donorInfoLabel = new Label
            {
                Text = "Select a donor file with free space",
                Location = new System.Drawing.Point(10, 255),
                Size = new System.Drawing.Size(400, 40),
                ForeColor = System.Drawing.Color.DarkGreen
            };
            donorGroupBox.Controls.Add(_donorInfoLabel);
            this.Controls.Add(donorGroupBox);

            // Recipient section
            var recipientGroupBox = new GroupBox
            {
                Text = "Recipient File (will receive space)",
                Location = new System.Drawing.Point(450, 40),
                Size = new System.Drawing.Size(420, 300)
            };

            _recipientListView = new ListView
            {
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(400, 230),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true
            };
            _recipientListView.Columns.Add("File Name", 200);
            _recipientListView.Columns.Add("Used", 70);
            _recipientListView.Columns.Add("Allocated", 70);
            _recipientListView.Columns.Add("Free", 50);
            _recipientListView.SelectedIndexChanged += RecipientListView_SelectedIndexChanged;
            recipientGroupBox.Controls.Add(_recipientListView);

            _recipientInfoLabel = new Label
            {
                Text = "Select a recipient file",
                Location = new System.Drawing.Point(10, 255),
                Size = new System.Drawing.Size(400, 40),
                ForeColor = System.Drawing.Color.DarkBlue
            };
            recipientGroupBox.Controls.Add(_recipientInfoLabel);
            this.Controls.Add(recipientGroupBox);

            // Transfer amount section
            var transferGroupBox = new GroupBox
            {
                Text = "Transfer Amount",
                Location = new System.Drawing.Point(12, 350),
                Size = new System.Drawing.Size(420, 80)
            };

            var bytesLabel = new Label
            {
                Text = "Bytes to transfer:",
                Location = new System.Drawing.Point(10, 30),
                AutoSize = true
            };
            transferGroupBox.Controls.Add(bytesLabel);

            _bytesToTransferNumeric = new NumericUpDown
            {
                Location = new System.Drawing.Point(120, 28),
                Size = new System.Drawing.Size(120, 23),
                Minimum = 1,
                Maximum = 1000000,
                Value = 100,
                Enabled = false
            };
            _bytesToTransferNumeric.ValueChanged += BytesToTransfer_ValueChanged;
            transferGroupBox.Controls.Add(_bytesToTransferNumeric);

            var maxLabel = new Label
            {
                Text = "(max available from donor)",
                Location = new System.Drawing.Point(250, 30),
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray
            };
            transferGroupBox.Controls.Add(maxLabel);
            this.Controls.Add(transferGroupBox);

            // Summary section
            var summaryGroupBox = new GroupBox
            {
                Text = "Summary",
                Location = new System.Drawing.Point(450, 350),
                Size = new System.Drawing.Size(420, 80)
            };

            _summaryLabel = new Label
            {
                Text = "Select donor and recipient files to see transfer summary.",
                Location = new System.Drawing.Point(10, 25),
                Size = new System.Drawing.Size(400, 45),
                ForeColor = System.Drawing.Color.DarkGray
            };
            summaryGroupBox.Controls.Add(_summaryLabel);
            this.Controls.Add(summaryGroupBox);

            // Buttons
            _transferButton = new Button
            {
                Text = "Transfer Space",
                Location = new System.Drawing.Point(650, 520),
                Size = new System.Drawing.Size(100, 30),
                Enabled = false
            };
            _transferButton.Click += TransferButton_Click;
            this.Controls.Add(_transferButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(760, 520),
                Size = new System.Drawing.Size(100, 30),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(_cancelButton);
            this.CancelButton = _cancelButton;

            // Adjust form size for the mode selection
            this.Size = new System.Drawing.Size(900, 600);

            // Transfer mode selection
            var modeGroupBox = new GroupBox
            {
                Text = "Transfer Mode",
                Location = new System.Drawing.Point(12, 440),
                Size = new System.Drawing.Size(858, 70)
            };

            _inPlaceRadio = new RadioButton
            {
                Text = "In-Place Transfer (Recommended) - Preserves ALL assets (weapons, images, xanims, etc.)",
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(600, 20),
                Checked = true
            };
            modeGroupBox.Controls.Add(_inPlaceRadio);

            _rebuildRadio = new RadioButton
            {
                Text = "Rebuild Zone - Only keeps raw files and localized entries (loses other assets)",
                Location = new System.Drawing.Point(10, 42),
                Size = new System.Drawing.Size(600, 20),
                Checked = false
            };
            modeGroupBox.Controls.Add(_rebuildRadio);

            this.Controls.Add(modeGroupBox);
        }

        private void PopulateListViews()
        {
            foreach (var node in _rawFileNodes.OrderBy(n => n.FileName))
            {
                int usedSize = node.RawFileBytes?.Length ?? 0;
                int allocatedSize = node.MaxSize;
                int freeSpace = allocatedSize - usedSize;

                // Donor list - only show files with free space
                if (freeSpace > 0)
                {
                    var donorItem = new ListViewItem(new[]
                    {
                        node.FileName,
                        usedSize.ToString("N0"),
                        allocatedSize.ToString("N0"),
                        freeSpace.ToString("N0")
                    });
                    donorItem.Tag = node;
                    _donorListView.Items.Add(donorItem);
                }

                // Recipient list - show all files
                var recipientItem = new ListViewItem(new[]
                {
                    node.FileName,
                    usedSize.ToString("N0"),
                    allocatedSize.ToString("N0"),
                    freeSpace.ToString("N0")
                });
                recipientItem.Tag = node;
                _recipientListView.Items.Add(recipientItem);
            }

            if (_donorListView.Items.Count == 0)
            {
                _donorInfoLabel.Text = "No files have unused space to donate.";
                _donorInfoLabel.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void DonorListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_donorListView.SelectedItems.Count > 0)
            {
                var node = (RawFileNode)_donorListView.SelectedItems[0].Tag;
                int freeSpace = node.MaxSize - (node.RawFileBytes?.Length ?? 0);
                _donorInfoLabel.Text = $"Selected: {node.FileName}\nFree space available: {freeSpace:N0} bytes";

                _bytesToTransferNumeric.Maximum = freeSpace;
                _bytesToTransferNumeric.Value = Math.Min(_bytesToTransferNumeric.Value, freeSpace);
                _bytesToTransferNumeric.Enabled = true;
            }
            else
            {
                _donorInfoLabel.Text = "Select a donor file with free space";
                _bytesToTransferNumeric.Enabled = false;
            }
            UpdateSummary();
        }

        private void RecipientListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_recipientListView.SelectedItems.Count > 0)
            {
                var node = (RawFileNode)_recipientListView.SelectedItems[0].Tag;
                _recipientInfoLabel.Text = $"Selected: {node.FileName}\nCurrent allocation: {node.MaxSize:N0} bytes";
            }
            else
            {
                _recipientInfoLabel.Text = "Select a recipient file";
            }
            UpdateSummary();
        }

        private void BytesToTransfer_ValueChanged(object sender, EventArgs e)
        {
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            RawFileNode donor = _donorListView.SelectedItems.Count > 0
                ? (RawFileNode)_donorListView.SelectedItems[0].Tag
                : null;
            RawFileNode recipient = _recipientListView.SelectedItems.Count > 0
                ? (RawFileNode)_recipientListView.SelectedItems[0].Tag
                : null;

            if (donor == null || recipient == null)
            {
                _summaryLabel.Text = "Select donor and recipient files to see transfer summary.";
                _summaryLabel.ForeColor = System.Drawing.Color.DarkGray;
                _transferButton.Enabled = false;
                return;
            }

            if (donor == recipient)
            {
                _summaryLabel.Text = "Donor and recipient cannot be the same file.";
                _summaryLabel.ForeColor = System.Drawing.Color.Red;
                _transferButton.Enabled = false;
                return;
            }

            int bytesToTransfer = (int)_bytesToTransferNumeric.Value;
            int newDonorSize = donor.MaxSize - bytesToTransfer;
            int newRecipientSize = recipient.MaxSize + bytesToTransfer;

            _summaryLabel.Text = $"Donor '{Path.GetFileName(donor.FileName)}': {donor.MaxSize:N0} -> {newDonorSize:N0} bytes\n" +
                                 $"Recipient '{Path.GetFileName(recipient.FileName)}': {recipient.MaxSize:N0} -> {newRecipientSize:N0} bytes";
            _summaryLabel.ForeColor = System.Drawing.Color.DarkGreen;
            _transferButton.Enabled = true;
        }

        private void TransferButton_Click(object sender, EventArgs e)
        {
            SelectedDonor = (RawFileNode)_donorListView.SelectedItems[0].Tag;
            SelectedRecipient = (RawFileNode)_recipientListView.SelectedItems[0].Tag;
            BytesToTransfer = (int)_bytesToTransferNumeric.Value;
            UseInPlaceTransfer = _inPlaceRadio.Checked;

            string modeText = UseInPlaceTransfer
                ? "This will modify the zone in-place, preserving all assets."
                : "This will rebuild the zone (only raw files and localized entries will be kept).";

            var result = MessageBox.Show(
                $"Transfer {BytesToTransfer:N0} bytes from '{Path.GetFileName(SelectedDonor.FileName)}' to '{Path.GetFileName(SelectedRecipient.FileName)}'?\n\n" +
                modeText,
                "Confirm Transfer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}
