using System.Drawing;
using System.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Read-only viewer for a StructuredDataDefSet (MW2 / IW4). The official source format isn't
    /// shipped, so this shows the parsed layout dump (enums, structs with property name : type @
    /// offset, indexed/enumed arrays, root type) produced by the IW4 pointer-walk.
    /// </summary>
    public sealed class StructuredDataDefViewerForm : Form
    {
        public StructuredDataDefViewerForm(StructuredDataDefAsset def)
        {
            Text = $"Struct Data: {def.Name}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 640);
            MinimumSize = new Size(460, 340);

            var text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Both,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Text = def.DumpText,
                BackColor = SystemColors.Window,
            };

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(10, 8, 10, 8),
                Text = $"{def.Name}\n" +
                       $"defs: {def.DefCount}   enums: {def.EnumCount}   structs: {def.StructCount}   " +
                       $"offset: 0x{def.Offset:X}   —   read-only (IW4 layout dump)",
            };

            var copyButton = new Button { Text = "Copy", Dock = DockStyle.Right, Width = 90 };
            copyButton.Click += (_, _) =>
            {
                if (!string.IsNullOrEmpty(text.Text))
                    Clipboard.SetText(text.Text);
            };
            var closeButton = new Button { Text = "Close", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.OK };

            var buttonBar = new Panel { Dock = DockStyle.Bottom, Height = 36 };
            buttonBar.Controls.Add(closeButton);
            buttonBar.Controls.Add(copyButton);

            Controls.Add(text);
            Controls.Add(header);
            Controls.Add(buttonBar);
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }
    }
}
