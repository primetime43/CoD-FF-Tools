using System.Drawing;
using System.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Read-only viewer for an IW4 (MW2 PS3) weapon's parsed structure. The IW4 reader is
    /// byte-faithful (no per-field write offsets) and the classic weapType/weapClass enums aren't
    /// recovered, so this is a view — not an editor. Shows the WeaponVariantDef + WeaponDef fields
    /// the pointer-walk exposes (clip/fire/ADS, arcs, ranges, accuracy, turn speeds, strings, flags).
    /// </summary>
    public sealed class WeaponDetailForm : Form
    {
        public WeaponDetailForm(WeaponAsset weapon)
        {
            Text = $"Weapon: {weapon.InternalName}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(640, 600);
            MinimumSize = new Size(420, 320);

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                BackgroundColor = SystemColors.Window,
            };

            var fieldCol = new DataGridViewTextBoxColumn
            {
                HeaderText = "Field",
                FillWeight = 35,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            var valueCol = new DataGridViewTextBoxColumn
            {
                HeaderText = "Value",
                FillWeight = 65,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            grid.Columns.Add(fieldCol);
            grid.Columns.Add(valueCol);

            foreach (var (label, value) in weapon.DetailFields)
            {
                if (string.IsNullOrEmpty(label))
                {
                    // Section header row: bold the value cell holding the section name.
                    int idx = grid.Rows.Add(string.Empty, value);
                    var row = grid.Rows[idx];
                    row.DefaultCellStyle.BackColor = SystemColors.ControlLight;
                    row.DefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
                }
                else
                {
                    grid.Rows.Add(label, string.IsNullOrEmpty(value) ? "—" : value);
                }
            }

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(10, 8, 10, 8),
                Text = $"{weapon.InternalName}" +
                       (string.IsNullOrEmpty(weapon.DisplayName) ? "" : $"   ({weapon.DisplayName})") +
                       $"\nStart offset: 0x{weapon.StartOffset:X}   —   read-only (IW4 structure)",
                Font = new Font(Font, FontStyle.Regular),
            };

            var closeButton = new Button
            {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = 32,
                DialogResult = DialogResult.OK,
            };

            Controls.Add(grid);
            Controls.Add(header);
            Controls.Add(closeButton);
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }
    }
}
