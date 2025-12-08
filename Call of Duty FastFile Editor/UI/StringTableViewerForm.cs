using Call_of_Duty_FastFile_Editor.Models;
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Form for viewing StringTable data in a grid format.
    /// Displays the CSV-like data with proper columns and rows.
    /// </summary>
    public class StringTableViewerForm : Form
    {
        private readonly StringTable _stringTable;
        private DataGridView dataGridView;
        private Label infoLabel;
        private Button exportButton;
        private Button closeButton;

        public StringTableViewerForm(StringTable stringTable)
        {
            _stringTable = stringTable ?? throw new ArgumentNullException(nameof(stringTable));
            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            this.Text = $"StringTable Viewer - {_stringTable.TableName}";
            this.Size = new Size(900, 600);
            this.MinimumSize = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;

            // Info label at top
            infoLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(240, 240, 240)
            };

            // DataGridView for the table data
            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
                RowHeadersWidth = 50
            };

            // Button panel at bottom
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(5)
            };

            closeButton = new Button
            {
                Text = "Close",
                Width = 80
            };
            closeButton.Click += (s, e) => this.Close();

            exportButton = new Button
            {
                Text = "Export CSV",
                Width = 100,
                Margin = new Padding(5, 0, 0, 0)
            };
            exportButton.Click += ExportButton_Click;

            buttonPanel.Controls.Add(closeButton);
            buttonPanel.Controls.Add(exportButton);

            this.Controls.Add(dataGridView);
            this.Controls.Add(infoLabel);
            this.Controls.Add(buttonPanel);
        }

        private void PopulateData()
        {
            // Update info label
            int totalCells = _stringTable.RowCount * _stringTable.ColumnCount;
            int parsedCells = _stringTable.Cells?.Count ?? 0;
            infoLabel.Text = $"Table: {_stringTable.TableName}\n" +
                            $"Size: {_stringTable.RowCount} rows x {_stringTable.ColumnCount} columns = {totalCells} cells | " +
                            $"Parsed: {parsedCells} cells | " +
                            $"Offset: 0x{_stringTable.StartOfFileHeader:X}";

            // Clear existing data
            dataGridView.Columns.Clear();
            dataGridView.Rows.Clear();

            if (_stringTable.Cells == null || _stringTable.Cells.Count == 0)
            {
                infoLabel.Text += " | No cell data available";
                return;
            }

            int columnCount = _stringTable.ColumnCount;
            int rowCount = _stringTable.RowCount;

            // Add columns (Column 0, Column 1, etc. or use first row as headers)
            for (int col = 0; col < columnCount; col++)
            {
                var column = new DataGridViewTextBoxColumn
                {
                    HeaderText = $"Col {col}",
                    Name = $"Column{col}",
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                dataGridView.Columns.Add(column);
            }

            // Populate rows
            int cellIndex = 0;
            for (int row = 0; row < rowCount && cellIndex < _stringTable.Cells.Count; row++)
            {
                var rowData = new string[columnCount];
                for (int col = 0; col < columnCount && cellIndex < _stringTable.Cells.Count; col++)
                {
                    rowData[col] = _stringTable.Cells[cellIndex].Text;
                    cellIndex++;
                }
                int rowIndex = dataGridView.Rows.Add(rowData);
                dataGridView.Rows[rowIndex].HeaderCell.Value = row.ToString();
            }

            // If we have more cells than expected, add them as additional rows
            if (cellIndex < _stringTable.Cells.Count)
            {
                while (cellIndex < _stringTable.Cells.Count)
                {
                    var rowData = new string[columnCount];
                    for (int col = 0; col < columnCount && cellIndex < _stringTable.Cells.Count; col++)
                    {
                        rowData[col] = _stringTable.Cells[cellIndex].Text;
                        cellIndex++;
                    }
                    dataGridView.Rows.Add(rowData);
                }
            }

            // Try to use first row as headers if it looks like a header row
            if (dataGridView.Rows.Count > 0 && LooksLikeHeaderRow(0))
            {
                for (int col = 0; col < columnCount && col < dataGridView.Columns.Count; col++)
                {
                    var cellValue = dataGridView.Rows[0].Cells[col].Value?.ToString();
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        dataGridView.Columns[col].HeaderText = cellValue;
                    }
                }
                // Optionally remove the header row from data
                // dataGridView.Rows.RemoveAt(0);
            }
        }

        /// <summary>
        /// Checks if the first row looks like a header row (non-numeric, short strings).
        /// </summary>
        private bool LooksLikeHeaderRow(int rowIndex)
        {
            if (rowIndex >= dataGridView.Rows.Count) return false;

            var row = dataGridView.Rows[rowIndex];
            int nonNumericCount = 0;

            for (int col = 0; col < row.Cells.Count; col++)
            {
                var value = row.Cells[col].Value?.ToString() ?? "";
                // Header cells are typically short and non-numeric
                if (!string.IsNullOrEmpty(value) && value.Length < 50 && !double.TryParse(value, out _))
                {
                    nonNumericCount++;
                }
            }

            // If most cells are non-numeric short strings, it's likely a header
            return nonNumericCount > row.Cells.Count / 2;
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = System.IO.Path.GetFileNameWithoutExtension(_stringTable.TableName) + "_export.csv"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExportToCsv(saveDialog.FileName);
                    MessageBox.Show($"Exported to {saveDialog.FileName}", "Export Successful",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportToCsv(string filePath)
        {
            var sb = new StringBuilder();

            // Export headers
            var headers = new string[dataGridView.Columns.Count];
            for (int col = 0; col < dataGridView.Columns.Count; col++)
            {
                headers[col] = EscapeCsvField(dataGridView.Columns[col].HeaderText);
            }
            sb.AppendLine(string.Join(",", headers));

            // Export data rows
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                var cells = new string[dataGridView.Columns.Count];
                for (int col = 0; col < dataGridView.Columns.Count; col++)
                {
                    cells[col] = EscapeCsvField(row.Cells[col].Value?.ToString() ?? "");
                }
                sb.AppendLine(string.Join(",", cells));
            }

            System.IO.File.WriteAllText(filePath, sb.ToString());
        }

        private string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
