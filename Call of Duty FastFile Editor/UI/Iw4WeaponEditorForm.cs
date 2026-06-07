using System;
using System.Drawing;
using System.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Editor for an IW4 (MW2 PS3) weapon's validated scalar fields. Unlike the WaW-tuned
    /// <c>WeaponEditorForm</c> (0x9AC layout, would corrupt IW4), this writes only the fields whose
    /// byte offsets were verified against the zone: the enum block + damage/ammo in the WeaponDef, and
    /// clip/fire/ADS-fov in the variant. All reads/writes are big-endian (PS3). Values are patched
    /// directly into the in-memory zone; File → Save recompresses it.
    /// </summary>
    public sealed class Iw4WeaponEditorForm : Form
    {
        // Variant (WeaponVariantDef) field offsets, relative to WeaponAsset.StartOffset.
        private const int VarAdsZoomFov = 0x14;   // float
        private const int VarClipSize = 0x20;     // int
        private const int VarFireTime = 0x28;     // int
        // WeaponDef field offsets, relative to WeaponAsset.WeaponDefOffset (verified vs patch_mp.ff).
        private const int DefWeapType = 0x2C;
        private const int DefWeapClass = 0x30;
        private const int DefPenetrate = 0x34;
        private const int DefInventory = 0x38;
        private const int DefFireType = 0x3C;
        private const int DefStartAmmo = 0x208;
        private const int DefMaxAmmo = 0x21C;
        private const int DefDamage = 0x230;
        private const int DefMinDamage = 0x598;

        // Authoritative IW4 enum value lists (index = enum value).
        private static readonly string[] WeapType = { "bullet", "grenade", "projectile", "riotshield" };
        private static readonly string[] WeapClass = { "rifle", "sniper", "mg", "smg", "spread (shotgun)", "pistol", "grenade", "rocketlauncher", "turret", "throwingknife", "non-player", "item" };
        private static readonly string[] PenetrateType = { "none", "small", "medium", "large" };
        private static readonly string[] InventoryType = { "primary", "offhand", "item", "altmode", "exclusive", "scavenger" };
        private static readonly string[] FireType = { "fullauto", "singleshot", "burst2", "burst3", "burst4", "doublebarrel" };

        private readonly WeaponAsset _weapon;
        private readonly byte[] _zone;
        private readonly bool _hasDef;

        private ComboBox _cbType = null!, _cbClass = null!, _cbFire = null!, _cbPen = null!, _cbInv = null!;
        private NumericUpDown _nDamage = null!, _nMinDamage = null!, _nMaxAmmo = null!, _nStartAmmo = null!, _nClip = null!, _nFireTime = null!, _nAdsFov = null!;

        public bool ChangesSaved { get; private set; }

        public Iw4WeaponEditorForm(WeaponAsset weapon, byte[] zoneData)
        {
            _weapon = weapon;
            _zone = zoneData;
            _hasDef = weapon.WeaponDefOffset > 0;

            Text = $"Edit Weapon: {weapon.InternalName}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 620);
            MinimumSize = new Size(440, 480);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12, 10, 12, 10),
                AutoScroll = true,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            void AddHeader(string text)
            {
                var lbl = new Label { Text = text, Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 10, 0, 4) };
                layout.Controls.Add(lbl);
                layout.SetColumnSpan(lbl, 2);
            }
            ComboBox AddCombo(string label, string[] items, int value)
            {
                layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) });
                var cb = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = _hasDef };
                cb.Items.AddRange(items);
                cb.SelectedIndex = value >= 0 && value < items.Length ? value : -1;
                layout.Controls.Add(cb);
                return cb;
            }
            NumericUpDown AddNum(string label, decimal value, bool enabled, decimal min = 0, decimal max = 100000, int decimals = 0)
            {
                layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) });
                var n = new NumericUpDown { Dock = DockStyle.Fill, Minimum = min, Maximum = max, DecimalPlaces = decimals, Enabled = enabled };
                n.Value = Math.Min(Math.Max(value, min), max);
                layout.Controls.Add(n);
                return n;
            }

            AddHeader("Identity");
            layout.Controls.Add(new Label { Text = "internalName", AutoSize = true, Anchor = AnchorStyles.Left });
            layout.Controls.Add(new Label { Text = weapon.InternalName + (string.IsNullOrEmpty(weapon.DisplayName) ? "" : $"   ({weapon.DisplayName})"), AutoSize = true, Anchor = AnchorStyles.Left });

            AddHeader("WeaponDef enums" + (_hasDef ? "" : "  (WeaponDef not inline — read-only)"));
            int defB = weapon.WeaponDefOffset;
            _cbType = AddCombo("type", WeapType, _hasDef ? ReadInt(defB + DefWeapType) : -1);
            _cbClass = AddCombo("class", WeapClass, _hasDef ? ReadInt(defB + DefWeapClass) : -1);
            _cbFire = AddCombo("fireType", FireType, _hasDef ? ReadInt(defB + DefFireType) : -1);
            _cbPen = AddCombo("penetrateType", PenetrateType, _hasDef ? ReadInt(defB + DefPenetrate) : -1);
            _cbInv = AddCombo("inventoryType", InventoryType, _hasDef ? ReadInt(defB + DefInventory) : -1);

            AddHeader("Damage / Ammo");
            _nDamage = AddNum("damage", _hasDef ? ReadInt(defB + DefDamage) : 0, _hasDef);
            _nMinDamage = AddNum("minDamage", _hasDef ? ReadInt(defB + DefMinDamage) : 0, _hasDef);
            _nMaxAmmo = AddNum("maxAmmo (reserve)", _hasDef ? ReadInt(defB + DefMaxAmmo) : 0, _hasDef);
            _nStartAmmo = AddNum("startAmmo", _hasDef ? ReadInt(defB + DefStartAmmo) : 0, _hasDef);

            AddHeader("Variant");
            int varB = weapon.StartOffset;
            _nClip = AddNum("clipSize", ReadInt(varB + VarClipSize), true);
            _nFireTime = AddNum("fireTime (ms)", ReadInt(varB + VarFireTime), true);
            _nAdsFov = AddNum("adsZoomFov", (decimal)ReadFloat(varB + VarAdsZoomFov), true, 1, 160, 2);

            var save = new Button { Text = "Save", Dock = DockStyle.Right, Width = 100, DialogResult = DialogResult.OK };
            save.Click += SaveClick;
            var cancel = new Button { Text = "Cancel", Dock = DockStyle.Right, Width = 100, DialogResult = DialogResult.Cancel };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
            bar.Controls.Add(save);
            bar.Controls.Add(cancel);

            Controls.Add(layout);
            Controls.Add(bar);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void SaveClick(object? sender, EventArgs e)
        {
            int varB = _weapon.StartOffset;
            WriteInt(varB + VarClipSize, (int)_nClip.Value);
            WriteInt(varB + VarFireTime, (int)_nFireTime.Value);
            WriteFloat(varB + VarAdsZoomFov, (float)_nAdsFov.Value);

            if (_hasDef)
            {
                int defB = _weapon.WeaponDefOffset;
                if (_cbType.SelectedIndex >= 0) WriteInt(defB + DefWeapType, _cbType.SelectedIndex);
                if (_cbClass.SelectedIndex >= 0) WriteInt(defB + DefWeapClass, _cbClass.SelectedIndex);
                if (_cbFire.SelectedIndex >= 0) WriteInt(defB + DefFireType, _cbFire.SelectedIndex);
                if (_cbPen.SelectedIndex >= 0) WriteInt(defB + DefPenetrate, _cbPen.SelectedIndex);
                if (_cbInv.SelectedIndex >= 0) WriteInt(defB + DefInventory, _cbInv.SelectedIndex);
                WriteInt(defB + DefDamage, (int)_nDamage.Value);
                WriteInt(defB + DefMinDamage, (int)_nMinDamage.Value);
                WriteInt(defB + DefMaxAmmo, (int)_nMaxAmmo.Value);
                WriteInt(defB + DefStartAmmo, (int)_nStartAmmo.Value);
            }

            // Keep the in-memory model consistent so the grid refreshes correctly.
            _weapon.ClipSize = (int)_nClip.Value;
            _weapon.FireTime = (int)_nFireTime.Value;
            _weapon.AdsZoomFov = (float)_nAdsFov.Value;
            if (_hasDef)
            {
                _weapon.Damage = (int)_nDamage.Value;
                _weapon.MinDamage = (int)_nMinDamage.Value;
                _weapon.MaxAmmo = (int)_nMaxAmmo.Value;
                if (_cbType.SelectedIndex >= 0) _weapon.TypeName = WeapType[_cbType.SelectedIndex];
                if (_cbClass.SelectedIndex >= 0) _weapon.ClassName = WeapClass[_cbClass.SelectedIndex];
                if (_cbFire.SelectedIndex >= 0) _weapon.FireTypeName = FireType[_cbFire.SelectedIndex];
                if (_cbPen.SelectedIndex >= 0) _weapon.PenetrateName = PenetrateType[_cbPen.SelectedIndex];
                if (_cbInv.SelectedIndex >= 0) _weapon.InventoryName = InventoryType[_cbInv.SelectedIndex];
            }

            ChangesSaved = true;
        }

        private int ReadInt(int o)
            => (o >= 0 && o + 4 <= _zone.Length)
                ? (_zone[o] << 24) | (_zone[o + 1] << 16) | (_zone[o + 2] << 8) | _zone[o + 3]
                : 0;

        private float ReadFloat(int o) => BitConverter.Int32BitsToSingle(ReadInt(o));

        private void WriteInt(int o, int v)
        {
            if (o < 0 || o + 4 > _zone.Length) return;
            _zone[o] = (byte)(v >> 24);
            _zone[o + 1] = (byte)(v >> 16);
            _zone[o + 2] = (byte)(v >> 8);
            _zone[o + 3] = (byte)v;
        }

        private void WriteFloat(int o, float v) => WriteInt(o, BitConverter.SingleToInt32Bits(v));
    }
}
