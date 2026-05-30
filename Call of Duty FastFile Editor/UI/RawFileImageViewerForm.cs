using Call_of_Duty_FastFile_Editor.Models;
using System.Drawing.Imaging;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Modal viewer for raw files whose bytes are a native image format
    /// (JPEG, PNG, BMP, GIF, TIFF, ICO). Unlike <see cref="ImagePreviewForm"/>
    /// — which decodes the IW <c>image</c> asset type and handles DXT etc. —
    /// this viewer just hands the bytes to <see cref="Bitmap"/> which handles
    /// the standard formats natively. Used for the surprisingly common case
    /// of `.jpg`/`.png` etc. shipped as <c>rawfile</c> assets inside FFs
    /// (e.g. Ghosts <c>ui_mp/ingamestore/img_store_*.jpg</c>).
    /// </summary>
    public class RawFileImageViewerForm : Form
    {
        private readonly RawFileNode _node;
        private readonly PictureBox _pictureBox;
        private readonly Label _infoLabel;
        private readonly Label _statusLabel;
        private readonly Button _saveButton;

        /// <summary>Image extensions <see cref="Bitmap"/> can decode directly.</summary>
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".ico"
        };

        /// <summary>True if <paramref name="fileName"/> has an extension that
        /// can be decoded by <see cref="Bitmap"/> natively.</summary>
        public static bool IsImageRawFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return SupportedExtensions.Contains(Path.GetExtension(fileName));
        }

        public RawFileImageViewerForm(RawFileNode node)
        {
            _node = node;
            Text = $"Image Preview — {node.FileName}";
            Size = new Size(900, 700);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(400, 300);

            _infoLabel = new Label
            {
                Dock = DockStyle.Top,
                // Two lines of Consolas 9pt + 10px top/bottom padding = ~58.
                // Use 64 for comfortable headroom so descenders/wider fonts
                // don't clip on high-DPI displays.
                Height = 64,
                Padding = new Padding(10),
                BackColor = SystemColors.ControlLight,
                Font = new Font("Consolas", 9),
            };

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 35 };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _saveButton = new Button
            {
                Text = "Save As…",
                Dock = DockStyle.Right,
                Width = 110,
                Enabled = false,
            };
            _saveButton.Click += SaveButton_Click;
            bottomPanel.Controls.Add(_statusLabel);
            bottomPanel.Controls.Add(_saveButton);

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.DarkGray,
            };

            Controls.Add(_pictureBox);
            Controls.Add(bottomPanel);
            Controls.Add(_infoLabel);

            LoadImage();
        }

        private void LoadImage()
        {
            byte[]? bytes = _node.RawFileBytes;
            if (bytes == null || bytes.Length == 0)
            {
                _infoLabel.Text = $"Name: {_node.FileName}\nSize: 0 bytes";
                _statusLabel.Text = "No bytes available.";
                _statusLabel.ForeColor = Color.Red;
                return;
            }

            try
            {
                // Copy the bytes into the MemoryStream because Bitmap keeps a
                // reference to the underlying stream for its lifetime — we
                // can't dispose the stream until the bitmap is gone. Easiest
                // is to keep the stream alive by attaching it to the bitmap's
                // Tag, but since this is a short-lived modal we just leave
                // the stream around for the form's lifetime.
                var ms = new MemoryStream(bytes, writable: false);
                var bitmap = new Bitmap(ms);
                _pictureBox.Image = bitmap;
                _saveButton.Enabled = true;

                _infoLabel.Text =
                    $"Name: {_node.FileName}\n" +
                    $"Format: {bitmap.RawFormat}  |  Dimensions: {bitmap.Width} × {bitmap.Height}  |  Bytes: {bytes.Length:N0}";
                _statusLabel.Text = "Image decoded successfully.";
                _statusLabel.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                _infoLabel.Text = $"Name: {_node.FileName}\nSize: {bytes.Length:N0} bytes";
                _statusLabel.Text = $"Could not decode as image: {ex.Message}";
                _statusLabel.ForeColor = Color.Red;
            }
        }

        /// <summary>
        /// Save As writes the <b>original</b> bytes verbatim (preserves the
        /// FF-shipped format byte-for-byte). If the user picks a different
        /// extension we re-encode through <see cref="Bitmap.Save(string, ImageFormat)"/>.
        /// </summary>
        private void SaveButton_Click(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Save Image",
                FileName = Path.GetFileName(_node.FileName),
                Filter = "Original Bytes|*.*|PNG|*.png|JPEG|*.jpg|Bitmap|*.bmp|GIF|*.gif",
                DefaultExt = Path.GetExtension(_node.FileName)?.TrimStart('.') ?? "png",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string outExt = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                string srcExt = Path.GetExtension(_node.FileName).ToLowerInvariant();
                // If the output extension matches the source, dump bytes verbatim.
                // Otherwise re-encode through the bitmap.
                if (outExt == srcExt || dlg.FilterIndex == 1 /* "Original Bytes" */)
                {
                    File.WriteAllBytes(dlg.FileName, _node.RawFileBytes ?? Array.Empty<byte>());
                }
                else
                {
                    ImageFormat fmt = outExt switch
                    {
                        ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                        ".bmp" => ImageFormat.Bmp,
                        ".gif" => ImageFormat.Gif,
                        _ => ImageFormat.Png,
                    };
                    _pictureBox.Image!.Save(dlg.FileName, fmt);
                }
                _statusLabel.Text = $"Saved to: {dlg.FileName}";
                _statusLabel.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save image: {ex.Message}", "Save Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pictureBox?.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
