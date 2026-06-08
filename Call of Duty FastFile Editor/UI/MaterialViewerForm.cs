using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Read-only viewer for a shader <see cref="MaterialAsset"/>. For pattern-scanned materials
    /// (CoD4 / WaW / etc.) only the name is known; for MW2 PS3 materials read by the IW4 pointer-walk
    /// it also shows the texture/constant/state-bit counts, the technique set, and — when those tables
    /// are stored inline — the per-texture (semantic : image) and per-constant detail.
    /// </summary>
    public sealed class MaterialViewerForm : Form
    {
        public MaterialViewerForm(MaterialAsset mat)
        {
            Text = $"Material: {mat.Name}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(640, 560);
            MinimumSize = new Size(420, 320);

            var text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Both,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Text = BuildDump(mat),
                BackColor = SystemColors.Window,
            };

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(10, 8, 10, 8),
                Text = $"{mat.Name}\n" +
                       $"offset: 0x{mat.StartOfFileHeader:X}   —   source: " +
                       (string.IsNullOrEmpty(mat.AdditionalData) ? "unknown" : mat.AdditionalData) +
                       (mat.IsStructuredView ? "" : "   (name only — full detail needs the IW4 walk)"),
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

        private static string BuildDump(MaterialAsset mat)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// {mat.Name}");
            sb.AppendLine($"offset        : 0x{mat.StartOfFileHeader:X}");
            sb.AppendLine($"source        : {mat.AdditionalData}");

            if (!mat.IsStructuredView)
            {
                sb.AppendLine();
                sb.AppendLine("This material was located by the pattern scanner, which only recovers the");
                sb.AppendLine("name. Texture/constant/technique detail is available for MW2 PS3 zones whose");
                sb.AppendLine("full IW4 pointer-walk completes.");
                return sb.ToString();
            }

            sb.AppendLine($"techniqueSet  : {(string.IsNullOrEmpty(mat.TechniqueSetName) ? "<shared / not resolved>" : mat.TechniqueSetName)}");
            sb.AppendLine($"textureCount  : {mat.TextureCount}");
            sb.AppendLine($"constantCount : {mat.ConstantCount}");
            sb.AppendLine($"stateBitCount : {mat.StateBitsCount}");
            sb.AppendLine();

            sb.AppendLine($"textures ({mat.Textures.Count} resolved inline of {mat.TextureCount}):");
            if (mat.Textures.Count == 0)
                sb.AppendLine("  (none stored inline — the texture table is a shared/offset pointer)");
            else
                foreach (var t in mat.Textures)
                    sb.AppendLine($"  {t}");
            sb.AppendLine();

            sb.AppendLine($"constants ({mat.Constants.Count} resolved inline of {mat.ConstantCount}):");
            if (mat.Constants.Count == 0)
                sb.AppendLine("  (none stored inline)");
            else
                foreach (var c in mat.Constants)
                    sb.AppendLine($"  {c}");

            if (mat.Techniques.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"techniques ({mat.Techniques.Count}):");
                foreach (var tech in mat.Techniques)
                    sb.AppendLine($"  {tech}");
            }

            return sb.ToString();
        }
    }
}
