using Call_of_Duty_FastFile_Editor.Models;
using System;
using System.Windows.Forms;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Dialog form for editing weapon asset values.
    /// </summary>
    public partial class WeaponEditorForm : Form
    {
        private readonly WeaponAsset _weapon;
        private readonly byte[] _zoneData;
        private readonly int _alignmentAdjust;

        // Field offsets from header start (WaW/CoD5)
        private const int OFFSET_PENETRATE_TYPE = 0x140;
        private const int OFFSET_IMPACT_TYPE = 0x148;
        private const int OFFSET_FIRE_TYPE = 0x14C;
        private const int OFFSET_WEAP_CLASS = 0x150;
        private const int OFFSET_INVENTORY_TYPE = 0x158;
        private const int OFFSET_DAMAGE = 0x3EC;
        private const int OFFSET_MAX_AMMO = 0x404;
        private const int OFFSET_CLIP_SIZE = 0x408;

        /// <summary>
        /// Gets whether changes were made and saved.
        /// </summary>
        public bool ChangesSaved { get; private set; }

        public WeaponEditorForm(WeaponAsset weapon, byte[] zoneData)
        {
            _weapon = weapon;
            _zoneData = zoneData;
            _alignmentAdjust = DetectAlignmentAdjust();

            InitializeComponent();
            LoadWeaponData();
        }

        private void InitializeComponent()
        {
            this.Text = $"Edit Weapon: {_weapon.InternalName}";
            this.Size = new System.Drawing.Size(450, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 15;
            int labelWidth = 120;
            int inputWidth = 280;
            int rowHeight = 32;

            // Internal Name (read-only)
            AddLabel("Internal Name:", 15, y, labelWidth);
            var txtInternalName = AddTextBox(_weapon.InternalName, 140, y, inputWidth);
            txtInternalName.Name = "txtInternalName";
            txtInternalName.ReadOnly = true;
            txtInternalName.BackColor = System.Drawing.SystemColors.Control;
            y += rowHeight;

            // Display Name (read-only for now)
            AddLabel("Display Name:", 15, y, labelWidth);
            var txtDisplayName = AddTextBox(_weapon.DisplayName, 140, y, inputWidth);
            txtDisplayName.Name = "txtDisplayName";
            txtDisplayName.ReadOnly = true;
            txtDisplayName.BackColor = System.Drawing.SystemColors.Control;
            y += rowHeight;

            // Separator
            y += 10;
            var separator1 = new Label
            {
                Text = "Weapon Properties",
                Font = new System.Drawing.Font(this.Font, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(15, y),
                AutoSize = true
            };
            this.Controls.Add(separator1);
            y += 25;

            // Weapon Class
            AddLabel("Weapon Class:", 15, y, labelWidth);
            var cmbWeapClass = AddComboBox<WeaponClass>(140, y, inputWidth);
            cmbWeapClass.Name = "cmbWeapClass";
            y += rowHeight;

            // Fire Type
            AddLabel("Fire Type:", 15, y, labelWidth);
            var cmbFireType = AddComboBox<WeaponFireType>(140, y, inputWidth);
            cmbFireType.Name = "cmbFireType";
            y += rowHeight;

            // Penetrate Type
            AddLabel("Penetrate Type:", 15, y, labelWidth);
            var cmbPenetrateType = AddComboBox<PenetrateType>(140, y, inputWidth);
            cmbPenetrateType.Name = "cmbPenetrateType";
            y += rowHeight;

            // Impact Type
            AddLabel("Impact Type:", 15, y, labelWidth);
            var cmbImpactType = AddComboBox<ImpactType>(140, y, inputWidth);
            cmbImpactType.Name = "cmbImpactType";
            y += rowHeight;

            // Inventory Type
            AddLabel("Inventory Type:", 15, y, labelWidth);
            var cmbInventoryType = AddComboBox<WeaponInventoryType>(140, y, inputWidth);
            cmbInventoryType.Name = "cmbInventoryType";
            y += rowHeight;

            // Separator
            y += 10;
            var separator2 = new Label
            {
                Text = "Numeric Values",
                Font = new System.Drawing.Font(this.Font, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(15, y),
                AutoSize = true
            };
            this.Controls.Add(separator2);
            y += 25;

            // Damage
            AddLabel("Damage:", 15, y, labelWidth);
            var numDamage = AddNumericUpDown(140, y, 120, 0, 500);
            numDamage.Name = "numDamage";
            y += rowHeight;

            // Clip Size
            AddLabel("Clip Size:", 15, y, labelWidth);
            var numClipSize = AddNumericUpDown(140, y, 120, 0, 500);
            numClipSize.Name = "numClipSize";
            y += rowHeight;

            // Max Ammo
            AddLabel("Max Ammo:", 15, y, labelWidth);
            var numMaxAmmo = AddNumericUpDown(140, y, 120, 0, 999);
            numMaxAmmo.Name = "numMaxAmmo";
            y += rowHeight;

            // Offset info (read-only)
            y += 10;
            var lblOffsetInfo = new Label
            {
                Text = $"Start Offset: 0x{_weapon.StartOffset:X}  |  Alignment Adjust: {_alignmentAdjust}",
                Location = new System.Drawing.Point(15, y),
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray
            };
            this.Controls.Add(lblOffsetInfo);
            y += 30;

            // Buttons
            var btnSave = new Button
            {
                Text = "Save Changes",
                Location = new System.Drawing.Point(140, y),
                Size = new System.Drawing.Size(120, 30)
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(270, y),
                Size = new System.Drawing.Size(100, 30),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private Label AddLabel(string text, int x, int y, int width)
        {
            var label = new Label
            {
                Text = text,
                Location = new System.Drawing.Point(x, y + 3),
                Size = new System.Drawing.Size(width, 20),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight
            };
            this.Controls.Add(label);
            return label;
        }

        private TextBox AddTextBox(string text, int x, int y, int width)
        {
            var textBox = new TextBox
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(width, 23)
            };
            this.Controls.Add(textBox);
            return textBox;
        }

        private ComboBox AddComboBox<T>(int x, int y, int width) where T : Enum
        {
            var comboBox = new ComboBox
            {
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(width, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Add enum values (excluding "Num" and "Count" entries)
            foreach (T value in Enum.GetValues(typeof(T)))
            {
                string name = value.ToString();
                if (name != "Num" && name != "Count")
                {
                    comboBox.Items.Add(value);
                }
            }

            this.Controls.Add(comboBox);
            return comboBox;
        }

        private NumericUpDown AddNumericUpDown(int x, int y, int width, int min, int max)
        {
            var numericUpDown = new NumericUpDown
            {
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(width, 23),
                Minimum = min,
                Maximum = max
            };
            this.Controls.Add(numericUpDown);
            return numericUpDown;
        }

        private void LoadWeaponData()
        {
            // Set combo box values
            var cmbWeapClass = (ComboBox)Controls["cmbWeapClass"];
            var cmbFireType = (ComboBox)Controls["cmbFireType"];
            var cmbPenetrateType = (ComboBox)Controls["cmbPenetrateType"];
            var cmbImpactType = (ComboBox)Controls["cmbImpactType"];
            var cmbInventoryType = (ComboBox)Controls["cmbInventoryType"];

            cmbWeapClass.SelectedItem = _weapon.WeapClass;
            cmbFireType.SelectedItem = _weapon.FireType;
            cmbPenetrateType.SelectedItem = _weapon.PenetrateType;
            cmbImpactType.SelectedItem = _weapon.ImpactType;
            cmbInventoryType.SelectedItem = _weapon.InventoryType;

            // Set numeric values
            var numDamage = (NumericUpDown)Controls["numDamage"];
            var numClipSize = (NumericUpDown)Controls["numClipSize"];
            var numMaxAmmo = (NumericUpDown)Controls["numMaxAmmo"];

            numDamage.Value = Math.Max(0, _weapon.Damage);
            numClipSize.Value = Math.Max(0, _weapon.ClipSize);
            numMaxAmmo.Value = Math.Max(0, _weapon.MaxAmmo);
        }

        private int DetectAlignmentAdjust()
        {
            int offset = _weapon.StartOffset;
            if (offset + 8 <= _zoneData.Length)
            {
                int ffCount = 0;
                for (int i = 0; i < 8 && _zoneData[offset + i] == 0xFF; i++)
                {
                    ffCount++;
                }
                if (ffCount == 6)
                {
                    return 2;
                }
            }
            return 0;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                // Get values from controls
                var cmbWeapClass = (ComboBox)Controls["cmbWeapClass"];
                var cmbFireType = (ComboBox)Controls["cmbFireType"];
                var cmbPenetrateType = (ComboBox)Controls["cmbPenetrateType"];
                var cmbImpactType = (ComboBox)Controls["cmbImpactType"];
                var cmbInventoryType = (ComboBox)Controls["cmbInventoryType"];
                var numDamage = (NumericUpDown)Controls["numDamage"];
                var numClipSize = (NumericUpDown)Controls["numClipSize"];
                var numMaxAmmo = (NumericUpDown)Controls["numMaxAmmo"];

                int adjustedOffset = _weapon.StartOffset + _alignmentAdjust;

                // Write enum values (as 4-byte big-endian integers)
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_PENETRATE_TYPE, (uint)(PenetrateType)cmbPenetrateType.SelectedItem);
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_IMPACT_TYPE, (uint)(ImpactType)cmbImpactType.SelectedItem);
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_FIRE_TYPE, (uint)(WeaponFireType)cmbFireType.SelectedItem);
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_WEAP_CLASS, (uint)(WeaponClass)cmbWeapClass.SelectedItem);
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_INVENTORY_TYPE, (uint)(WeaponInventoryType)cmbInventoryType.SelectedItem);

                // Write numeric values
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_DAMAGE, (uint)numDamage.Value);
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_MAX_AMMO, (uint)numMaxAmmo.Value);
                WriteUInt32BE(_zoneData, adjustedOffset + OFFSET_CLIP_SIZE, (uint)numClipSize.Value);

                // Update the weapon object
                _weapon.WeapClass = (WeaponClass)cmbWeapClass.SelectedItem;
                _weapon.FireType = (WeaponFireType)cmbFireType.SelectedItem;
                _weapon.PenetrateType = (PenetrateType)cmbPenetrateType.SelectedItem;
                _weapon.ImpactType = (ImpactType)cmbImpactType.SelectedItem;
                _weapon.InventoryType = (WeaponInventoryType)cmbInventoryType.SelectedItem;
                _weapon.Damage = (int)numDamage.Value;
                _weapon.ClipSize = (int)numClipSize.Value;
                _weapon.MaxAmmo = (int)numMaxAmmo.Value;

                ChangesSaved = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save weapon changes: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void WriteUInt32BE(byte[] data, int offset, uint value)
        {
            if (offset + 4 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside the data array bounds.");

            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }
    }
}
