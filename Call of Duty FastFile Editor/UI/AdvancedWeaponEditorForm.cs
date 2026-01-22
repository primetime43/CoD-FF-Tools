using Call_of_Duty_FastFile_Editor.GameDefinitions;
using Call_of_Duty_FastFile_Editor.Models;
using Call_of_Duty_FastFile_Editor.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Advanced weapon editor form with tabbed interface, search functionality,
    /// and support for 50+ weapon fields across multiple categories.
    /// </summary>
    public partial class AdvancedWeaponEditorForm : Form
    {
        private readonly WeaponAsset _weapon;
        private readonly byte[] _zoneData;
        private readonly IGameDefinition _gameDefinition;
        private readonly WeaponDataService _dataService;
        private readonly int _alignmentAdjust;
        private readonly ToolTip _toolTip;

        // UI Controls
        private TabControl _tabControl = null!;
        private TextBox _searchBox = null!;
        private Button _clearSearchButton = null!;
        private Label _statusLabel = null!;
        private Button _saveButton = null!;
        private Button _cancelButton = null!;

        // Track field controls for search and value access
        private readonly Dictionary<string, Control> _fieldControls = new();
        private readonly Dictionary<string, Label> _fieldLabels = new();
        private readonly Dictionary<string, WeaponFieldDefinition> _fieldDefinitions = new();

        // Change tracking
        private readonly HashSet<string> _changedFields = new();
        private readonly Dictionary<string, object?> _originalValues = new();
        private bool _isLoading = true; // Suppress change detection during initial load

        /// <summary>
        /// Gets whether changes were made and saved.
        /// </summary>
        public bool ChangesSaved { get; private set; }

        /// <summary>
        /// Creates a new AdvancedWeaponEditorForm.
        /// </summary>
        /// <param name="weapon">The weapon asset to edit.</param>
        /// <param name="zoneData">The zone data buffer.</param>
        /// <param name="gameDefinition">The game definition for platform detection.</param>
        public AdvancedWeaponEditorForm(WeaponAsset weapon, byte[] zoneData, IGameDefinition gameDefinition)
        {
            _weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            _zoneData = zoneData ?? throw new ArgumentNullException(nameof(zoneData));
            _gameDefinition = gameDefinition ?? throw new ArgumentNullException(nameof(gameDefinition));
            _dataService = new WeaponDataService(zoneData, gameDefinition);
            _alignmentAdjust = _dataService.DetectAlignmentAdjust(weapon.StartOffset);
            _toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 500, ReshowDelay = 200 };

            InitializeComponent();
            LoadWeaponData();
        }

        private void InitializeComponent()
        {
            this.Text = $"Advanced Weapon Editor - {_weapon.InternalName}";
            this.Size = new Size(800, 700);
            this.MinimumSize = new Size(700, 500);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Icon = SystemIcons.Application;

            // Main layout panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Search bar
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Tab control
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Status
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Buttons

            // Search bar panel
            var searchPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var searchLabel = new Label
            {
                Text = "Search:",
                AutoSize = true,
                Margin = new Padding(0, 5, 5, 0)
            };
            searchPanel.Controls.Add(searchLabel);

            _searchBox = new TextBox
            {
                Width = 300,
                Margin = new Padding(0, 2, 5, 0)
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            searchPanel.Controls.Add(_searchBox);

            _clearSearchButton = new Button
            {
                Text = "Clear",
                Width = 60,
                Margin = new Padding(0, 0, 0, 0)
            };
            _clearSearchButton.Click += (s, e) => { _searchBox.Clear(); };
            searchPanel.Controls.Add(_clearSearchButton);

            mainPanel.Controls.Add(searchPanel, 0, 0);

            // Tab control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            CreateCategoryTabs();
            mainPanel.Controls.Add(_tabControl, 0, 1);

            // Status panel
            var statusPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            _statusLabel = new Label
            {
                Text = $"Platform: {_dataService.Platform} | Game: {_dataService.GameShortName} | Changes: 0",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DarkGray
            };
            statusPanel.Controls.Add(_statusLabel);
            mainPanel.Controls.Add(statusPanel, 0, 2);

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(5, 5, 0, 0)
            };
            buttonPanel.Controls.Add(_cancelButton);

            _saveButton = new Button
            {
                Text = "Save Changes",
                Width = 120,
                Height = 30,
                Margin = new Padding(5, 5, 0, 0)
            };
            _saveButton.Click += SaveButton_Click;
            buttonPanel.Controls.Add(_saveButton);

            mainPanel.Controls.Add(buttonPanel, 0, 3);

            this.Controls.Add(mainPanel);

            this.AcceptButton = _saveButton;
            this.CancelButton = _cancelButton;

            this.FormClosing += AdvancedWeaponEditorForm_FormClosing;
        }

        private void CreateCategoryTabs()
        {
            // Get all available categories in order
            var categories = Enum.GetValues(typeof(WeaponFieldCategory))
                .Cast<WeaponFieldCategory>()
                .ToArray();

            foreach (var category in categories)
            {
                var fields = WeaponFieldRegistry.GetFieldsByCategory(category)
                    .Where(f => f.IsAvailableFor(_dataService.GameShortName))
                    .ToList();

                if (fields.Count == 0)
                    continue;

                var tabPage = new TabPage(WeaponFieldRegistry.GetCategoryDisplayName(category))
                {
                    AutoScroll = true,
                    Padding = new Padding(10)
                };

                CreateFieldControls(tabPage, fields);
                _tabControl.TabPages.Add(tabPage);
            }
        }

        private void CreateFieldControls(TabPage tabPage, List<WeaponFieldDefinition> fields)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(5)
            };

            // Column widths: Label, Control, Tooltip icon
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));

            int row = 0;
            foreach (var field in fields)
            {
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

                // Label
                var label = new Label
                {
                    Text = field.DisplayName + ":",
                    AutoSize = false,
                    Width = 190,
                    Height = 22,
                    TextAlign = ContentAlignment.MiddleRight,
                    Margin = new Padding(0, 3, 5, 0),
                    Tag = field.InternalName
                };
                panel.Controls.Add(label, 0, row);
                _fieldLabels[field.InternalName] = label;
                _fieldDefinitions[field.InternalName] = field;

                // Control based on field type
                Control control = CreateControlForField(field);
                control.Tag = field.InternalName;
                control.Margin = new Padding(0, 2, 0, 0);
                panel.Controls.Add(control, 1, row);
                _fieldControls[field.InternalName] = control;

                // Tooltip icon/label
                if (!string.IsNullOrEmpty(field.Tooltip))
                {
                    var infoLabel = new Label
                    {
                        Text = "?",
                        AutoSize = false,
                        Width = 20,
                        Height = 20,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BorderStyle = BorderStyle.FixedSingle,
                        Cursor = Cursors.Help,
                        Margin = new Padding(5, 3, 0, 0),
                        ForeColor = Color.DarkBlue
                    };
                    _toolTip.SetToolTip(infoLabel, field.Tooltip);
                    _toolTip.SetToolTip(control, field.Tooltip);
                    panel.Controls.Add(infoLabel, 2, row);
                }

                row++;
            }

            tabPage.Controls.Add(panel);
        }

        private Control CreateControlForField(WeaponFieldDefinition field)
        {
            switch (field.FieldType)
            {
                case WeaponFieldType.Enum:
                    return CreateEnumComboBox(field);

                case WeaponFieldType.Bool:
                    var checkBox = new CheckBox
                    {
                        Width = 20,
                        Height = 20,
                        Enabled = !field.IsReadOnly
                    };
                    checkBox.CheckedChanged += (s, e) => OnFieldValueChanged(field.InternalName);
                    return checkBox;

                case WeaponFieldType.Float:
                    var floatBox = new NumericUpDown
                    {
                        Width = 150,
                        DecimalPlaces = field.DecimalPlaces,
                        Minimum = (decimal)(field.MinValue ?? -1000000),
                        Maximum = (decimal)(field.MaxValue ?? 1000000),
                        Increment = field.DecimalPlaces >= 3 ? 0.001m : 0.1m,
                        Enabled = !field.IsReadOnly
                    };
                    floatBox.ValueChanged += (s, e) => OnFieldValueChanged(field.InternalName);
                    return floatBox;

                case WeaponFieldType.Int32:
                case WeaponFieldType.UInt32:
                case WeaponFieldType.Int16:
                case WeaponFieldType.UInt16:
                case WeaponFieldType.Byte:
                default:
                    var numBox = new NumericUpDown
                    {
                        Width = 150,
                        DecimalPlaces = 0,
                        Minimum = (decimal)(field.MinValue ?? (field.FieldType == WeaponFieldType.UInt32 || field.FieldType == WeaponFieldType.UInt16 || field.FieldType == WeaponFieldType.Byte ? 0 : int.MinValue)),
                        Maximum = (decimal)(field.MaxValue ?? int.MaxValue),
                        Enabled = !field.IsReadOnly
                    };
                    numBox.ValueChanged += (s, e) => OnFieldValueChanged(field.InternalName);
                    return numBox;
            }
        }

        private ComboBox CreateEnumComboBox(WeaponFieldDefinition field)
        {
            var comboBox = new ComboBox
            {
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = !field.IsReadOnly
            };

            if (field.EnumType != null)
            {
                foreach (var value in Enum.GetValues(field.EnumType))
                {
                    string name = value.ToString() ?? "";
                    // Skip "Num" and "Count" entries which are just for array sizing
                    if (name != "Num" && name != "Count")
                    {
                        comboBox.Items.Add(value);
                    }
                }
            }

            comboBox.SelectedIndexChanged += (s, e) => OnFieldValueChanged(field.InternalName);
            return comboBox;
        }

        private void LoadWeaponData()
        {
            _isLoading = true;

            foreach (var kvp in _fieldControls)
            {
                string fieldName = kvp.Key;
                Control control = kvp.Value;
                WeaponFieldDefinition field = _fieldDefinitions[fieldName];

                object? value = _dataService.ReadFieldValue(field, _weapon.StartOffset, _alignmentAdjust);
                _originalValues[fieldName] = value;

                SetControlValue(control, field, value);
            }

            _isLoading = false;
            UpdateStatusLabel();
        }

        private void SetControlValue(Control control, WeaponFieldDefinition field, object? value)
        {
            if (value == null)
            {
                control.Enabled = false;
                return;
            }

            switch (control)
            {
                case ComboBox comboBox when field.FieldType == WeaponFieldType.Enum:
                    if (field.EnumType != null)
                    {
                        try
                        {
                            object enumValue = Enum.ToObject(field.EnumType, Convert.ToInt32(value));
                            int index = comboBox.Items.IndexOf(enumValue);
                            if (index >= 0)
                                comboBox.SelectedIndex = index;
                        }
                        catch
                        {
                            if (comboBox.Items.Count > 0)
                                comboBox.SelectedIndex = 0;
                        }
                    }
                    break;

                case CheckBox checkBox:
                    checkBox.Checked = Convert.ToBoolean(value);
                    break;

                case NumericUpDown numBox:
                    try
                    {
                        decimal decValue = Convert.ToDecimal(value);
                        // Clamp to control's range
                        decValue = Math.Max(numBox.Minimum, Math.Min(numBox.Maximum, decValue));
                        numBox.Value = decValue;
                    }
                    catch
                    {
                        numBox.Value = numBox.Minimum;
                    }
                    break;
            }
        }

        private object? GetControlValue(Control control, WeaponFieldDefinition field)
        {
            return control switch
            {
                ComboBox comboBox when field.FieldType == WeaponFieldType.Enum =>
                    comboBox.SelectedItem != null ? Convert.ToInt32(comboBox.SelectedItem) : 0,
                CheckBox checkBox => checkBox.Checked,
                NumericUpDown numBox when field.FieldType == WeaponFieldType.Float => (float)numBox.Value,
                NumericUpDown numBox => (int)numBox.Value,
                _ => null
            };
        }

        private void OnFieldValueChanged(string fieldName)
        {
            // Ignore changes during initial load
            if (_isLoading)
                return;

            if (!_fieldDefinitions.TryGetValue(fieldName, out var field))
                return;

            if (!_fieldControls.TryGetValue(fieldName, out var control))
                return;

            object? currentValue = GetControlValue(control, field);
            object? originalValue = _originalValues.GetValueOrDefault(fieldName);

            bool changed = !Equals(currentValue, originalValue);
            if (changed)
                _changedFields.Add(fieldName);
            else
                _changedFields.Remove(fieldName);

            // Update label color to indicate change
            if (_fieldLabels.TryGetValue(fieldName, out var label))
            {
                label.ForeColor = changed ? Color.Blue : SystemColors.ControlText;
            }

            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            int changeCount = _changedFields.Count;
            string changeText = changeCount == 1 ? "1 unsaved change" : $"{changeCount} unsaved changes";
            _statusLabel.Text = $"Platform: {_dataService.Platform} | Game: {_dataService.GameShortName} | {changeText}";
            _statusLabel.ForeColor = changeCount > 0 ? Color.DarkOrange : Color.DarkGray;
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            string searchTerm = _searchBox.Text.Trim().ToLowerInvariant();

            foreach (var kvp in _fieldControls)
            {
                string fieldName = kvp.Key;
                Control control = kvp.Value;
                WeaponFieldDefinition field = _fieldDefinitions[fieldName];
                Label? label = _fieldLabels.GetValueOrDefault(fieldName);

                bool matches = string.IsNullOrEmpty(searchTerm) ||
                               field.DisplayName.ToLowerInvariant().Contains(searchTerm) ||
                               field.InternalName.ToLowerInvariant().Contains(searchTerm) ||
                               field.Tooltip.ToLowerInvariant().Contains(searchTerm);

                // Show/hide controls
                control.Visible = matches;
                if (label != null)
                    label.Visible = matches;

                // Find and update the info label visibility
                if (control.Parent is TableLayoutPanel tablePanel)
                {
                    var position = tablePanel.GetPositionFromControl(control);
                    if (position.Column >= 0 && position.Row >= 0)
                    {
                        var infoControl = tablePanel.GetControlFromPosition(2, position.Row);
                        if (infoControl != null)
                            infoControl.Visible = matches;
                    }
                }
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (_changedFields.Count == 0)
            {
                MessageBox.Show("No changes to save.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                int savedCount = 0;
                var failedFields = new List<string>();

                foreach (string fieldName in _changedFields.ToList())
                {
                    if (!_fieldDefinitions.TryGetValue(fieldName, out var field))
                        continue;

                    if (!_fieldControls.TryGetValue(fieldName, out var control))
                        continue;

                    object? value = GetControlValue(control, field);
                    if (value == null)
                        continue;

                    if (_dataService.WriteFieldValue(field, _weapon.StartOffset, value, _alignmentAdjust))
                    {
                        _originalValues[fieldName] = value;
                        savedCount++;

                        // Reset label color
                        if (_fieldLabels.TryGetValue(fieldName, out var label))
                            label.ForeColor = SystemColors.ControlText;
                    }
                    else
                    {
                        failedFields.Add(field.DisplayName);
                    }
                }

                _changedFields.Clear();
                UpdateStatusLabel();

                if (failedFields.Count > 0)
                {
                    MessageBox.Show($"Saved {savedCount} changes.\n\nFailed to save:\n{string.Join("\n", failedFields)}",
                        "Partial Save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    ChangesSaved = true;
                    MessageBox.Show($"Successfully saved {savedCount} changes.\n\nNote: Changes are in memory. Use File > Save to write to disk.",
                        "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save changes: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AdvancedWeaponEditorForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_changedFields.Count > 0 && !ChangesSaved)
            {
                var result = MessageBox.Show(
                    $"You have {_changedFields.Count} unsaved changes.\n\nDo you want to save before closing?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                switch (result)
                {
                    case DialogResult.Yes:
                        SaveButton_Click(null, EventArgs.Empty);
                        if (_changedFields.Count > 0)
                        {
                            // Save failed or was cancelled
                            e.Cancel = true;
                        }
                        break;
                    case DialogResult.Cancel:
                        e.Cancel = true;
                        break;
                    // DialogResult.No - close without saving
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
