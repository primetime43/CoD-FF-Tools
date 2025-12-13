using Call_of_Duty_FastFile_Editor.Models;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Form for previewing image assets from zone files.
    /// Supports decoding DXT1/DXT5 compressed textures.
    /// </summary>
    public partial class ImagePreviewForm : Form
    {
        private readonly ImageAsset _image;
        private PictureBox _pictureBox;
        private Label _infoLabel;
        private Label _statusLabel;
        private Button _saveButton;

        public ImagePreviewForm(ImageAsset image)
        {
            _image = image;
            InitializeComponents();
            LoadImage();
        }

        private void InitializeComponents()
        {
            this.Text = $"Image Preview - {_image.Name}";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(400, 300);

            // Info label at top
            _infoLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(10),
                BackColor = SystemColors.ControlLight,
                Font = new Font("Consolas", 9),
                Text = GetImageInfo()
            };

            // Bottom panel with status and save button
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 35
            };

            // Status label
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Save button
            _saveButton = new Button
            {
                Text = "Save Image...",
                Dock = DockStyle.Right,
                Width = 100,
                Enabled = false
            };
            _saveButton.Click += SaveButton_Click;

            bottomPanel.Controls.Add(_statusLabel);
            bottomPanel.Controls.Add(_saveButton);

            // Picture box for image
            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.DarkGray
            };

            this.Controls.Add(_pictureBox);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(_infoLabel);
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (_pictureBox.Image == null) return;

            using var saveDialog = new SaveFileDialog
            {
                Title = "Save Image",
                FileName = _image.Name,
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap|*.bmp|All Files|*.*",
                DefaultExt = "png"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ImageFormat format = ImageFormat.Png;
                    string ext = Path.GetExtension(saveDialog.FileName).ToLowerInvariant();
                    format = ext switch
                    {
                        ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                        ".bmp" => ImageFormat.Bmp,
                        ".gif" => ImageFormat.Gif,
                        _ => ImageFormat.Png
                    };

                    _pictureBox.Image.Save(saveDialog.FileName, format);
                    _statusLabel.Text = $"Saved to: {saveDialog.FileName}";
                    _statusLabel.ForeColor = Color.DarkGreen;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save image: {ex.Message}", "Save Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string GetImageInfo()
        {
            string formatName = GetFormatName(_image.TextureFormat);
            return $"Name: {_image.Name}\n" +
                   $"Dimensions: {_image.Width} x {_image.Height} x {_image.Depth}\n" +
                   $"Format: {formatName} (0x{_image.TextureFormat:X2})  |  Size: {_image.FormattedSize}  |  " +
                   $"Streaming: {(_image.IsStreaming ? "Yes" : "No")}  |  Category: {_image.Category}";
        }

        private string GetFormatName(byte format)
        {
            return format switch
            {
                0x81 => "B8",
                0x82 => "A1R5G5B5",
                0x83 => "A4R4G4B4",
                0x84 => "R5G6B5",
                0x85 => "A8R8G8B8",
                0x86 => "DXT1",
                0x87 => "DXT3",
                0x88 => "DXT5",
                0x8B => "G8B8",
                0x8F => "R5G5B5",
                0x94 => "D24S8",
                0x9A => "R32F",
                0x9F => "A8R8G8B8_LIN",
                _ => "Unknown"
            };
        }

        private void LoadImage()
        {
            if (_image.IsStreaming)
            {
                _statusLabel.Text = "Image data is streamed (stored at end of zone file) - preview not available.";
                _statusLabel.ForeColor = Color.DarkOrange;
                ShowPlaceholder("Streaming image - data not embedded in asset");
                return;
            }

            if (_image.RawData == null || _image.RawData.Length == 0)
            {
                _statusLabel.Text = "No image data available.";
                _statusLabel.ForeColor = Color.Red;
                ShowPlaceholder("No image data found");
                return;
            }

            try
            {
                Bitmap? bitmap = DecodeImage();
                if (bitmap != null)
                {
                    _pictureBox.Image = bitmap;
                    _statusLabel.Text = $"Image decoded successfully. Original size: {_image.RawData.Length} bytes";
                    _statusLabel.ForeColor = Color.DarkGreen;
                    _saveButton.Enabled = true;
                }
                else
                {
                    _statusLabel.Text = $"Unsupported format: {GetFormatName(_image.TextureFormat)} (0x{_image.TextureFormat:X2})";
                    _statusLabel.ForeColor = Color.Red;
                    ShowPlaceholder($"Format not supported: {GetFormatName(_image.TextureFormat)}");
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error decoding: {ex.Message}";
                _statusLabel.ForeColor = Color.Red;
                ShowPlaceholder($"Decode error: {ex.Message}");
            }
        }

        private void ShowPlaceholder(string message)
        {
            var placeholder = new Bitmap(_image.Width > 0 ? _image.Width : 256,
                                         _image.Height > 0 ? _image.Height : 256);
            using (var g = Graphics.FromImage(placeholder))
            {
                g.Clear(Color.FromArgb(40, 40, 40));
                using var font = new Font("Arial", 12);
                var size = g.MeasureString(message, font);
                g.DrawString(message, font, Brushes.White,
                    (placeholder.Width - size.Width) / 2,
                    (placeholder.Height - size.Height) / 2);
            }
            _pictureBox.Image = placeholder;
        }

        private Bitmap? DecodeImage()
        {
            if (_image.RawData == null) return null;

            // PS3 textures may be swizzled - try to decode with and without swizzling
            return _image.TextureFormat switch
            {
                0x86 => DecodeDXT1(_image.RawData, _image.Width, _image.Height, isBigEndian: true),
                0x87 => DecodeDXT3(_image.RawData, _image.Width, _image.Height, isBigEndian: true),
                0x88 => DecodeDXT5(_image.RawData, _image.Width, _image.Height, isBigEndian: true),
                0x85 => DecodeA8R8G8B8(_image.RawData, _image.Width, _image.Height),
                0x9F => DecodeA8R8G8B8(_image.RawData, _image.Width, _image.Height),
                _ => null
            };
        }

        private Bitmap DecodeA8R8G8B8(byte[] data, int width, int height)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int bytesNeeded = width * height * 4;
            if (data.Length >= bytesNeeded)
            {
                Marshal.Copy(data, 0, bmpData.Scan0, bytesNeeded);
            }

            bitmap.UnlockBits(bmpData);
            return bitmap;
        }

        private Bitmap DecodeDXT1(byte[] data, int width, int height, bool isBigEndian = false)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int blockSize = 8; // DXT1 block size
            int offset = 0;

            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (offset + blockSize > data.Length) break;
                    DecodeDXT1Block(data, offset, bitmap, bx * 4, by * 4, width, height, isBigEndian);
                    offset += blockSize;
                }
            }

            return bitmap;
        }

        private void DecodeDXT1Block(byte[] data, int offset, Bitmap bitmap, int blockX, int blockY, int width, int height, bool isBigEndian)
        {
            ushort c0, c1;
            uint bits;

            // DXT data appears to be stored in little-endian format even on PS3
            // Read as little-endian regardless of platform
            c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
            c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));
            bits = (uint)(data[offset + 4] | (data[offset + 5] << 8) |
                          (data[offset + 6] << 16) | (data[offset + 7] << 24));

            Color[] colors = new Color[4];
            colors[0] = RGB565ToColor(c0);
            colors[1] = RGB565ToColor(c1);

            if (c0 > c1)
            {
                colors[2] = InterpolateColor(colors[0], colors[1], 2, 1);
                colors[3] = InterpolateColor(colors[0], colors[1], 1, 2);
            }
            else
            {
                colors[2] = InterpolateColor(colors[0], colors[1], 1, 1);
                colors[3] = Color.Transparent;
            }

            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int destX = blockX + px;
                    int destY = blockY + py;
                    if (destX < width && destY < height)
                    {
                        int idx = (int)((bits >> ((py * 4 + px) * 2)) & 0x3);
                        bitmap.SetPixel(destX, destY, colors[idx]);
                    }
                }
            }
        }

        private Bitmap DecodeDXT3(byte[] data, int width, int height, bool isBigEndian = false)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int blockSize = 16; // DXT3 block size
            int offset = 0;

            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (offset + blockSize > data.Length) break;
                    DecodeDXT3Block(data, offset, bitmap, bx * 4, by * 4, width, height, isBigEndian);
                    offset += blockSize;
                }
            }

            return bitmap;
        }

        private void DecodeDXT3Block(byte[] data, int offset, Bitmap bitmap, int blockX, int blockY, int width, int height, bool isBigEndian)
        {
            // Alpha block (8 bytes) - explicit alpha, 4 bits per pixel
            ulong alphaBits = 0;
            for (int i = 0; i < 8; i++)
            {
                alphaBits |= (ulong)data[offset + i] << (i * 8);
            }

            // Color block (8 bytes at offset + 8)
            ushort c0, c1;
            uint bits;

            // DXT data appears to be stored in little-endian format even on PS3
            c0 = (ushort)(data[offset + 8] | (data[offset + 9] << 8));
            c1 = (ushort)(data[offset + 10] | (data[offset + 11] << 8));
            bits = (uint)(data[offset + 12] | (data[offset + 13] << 8) |
                          (data[offset + 14] << 16) | (data[offset + 15] << 24));

            Color[] colors = new Color[4];
            colors[0] = RGB565ToColor(c0);
            colors[1] = RGB565ToColor(c1);
            colors[2] = InterpolateColor(colors[0], colors[1], 2, 1);
            colors[3] = InterpolateColor(colors[0], colors[1], 1, 2);

            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int destX = blockX + px;
                    int destY = blockY + py;
                    if (destX < width && destY < height)
                    {
                        int pixelIdx = py * 4 + px;
                        int colorIdx = (int)((bits >> (pixelIdx * 2)) & 0x3);

                        // Extract 4-bit alpha value
                        int alphaIdx = (int)((alphaBits >> (pixelIdx * 4)) & 0xF);
                        int alpha = alphaIdx * 17; // Scale 0-15 to 0-255

                        Color c = colors[colorIdx];
                        bitmap.SetPixel(destX, destY, Color.FromArgb(alpha, c.R, c.G, c.B));
                    }
                }
            }
        }

        private Bitmap DecodeDXT5(byte[] data, int width, int height, bool isBigEndian = false)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int blockSize = 16; // DXT5 block size
            int offset = 0;

            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (offset + blockSize > data.Length) break;
                    DecodeDXT5Block(data, offset, bitmap, bx * 4, by * 4, width, height, isBigEndian);
                    offset += blockSize;
                }
            }

            return bitmap;
        }

        private void DecodeDXT5Block(byte[] data, int offset, Bitmap bitmap, int blockX, int blockY, int width, int height, bool isBigEndian)
        {
            // Alpha block (8 bytes)
            byte alpha0 = data[offset];
            byte alpha1 = data[offset + 1];
            ulong alphaBits = 0;
            for (int i = 2; i < 8; i++)
            {
                alphaBits |= (ulong)data[offset + i] << ((i - 2) * 8);
            }

            byte[] alphaValues = new byte[8];
            alphaValues[0] = alpha0;
            alphaValues[1] = alpha1;
            if (alpha0 > alpha1)
            {
                for (int i = 2; i < 8; i++)
                    alphaValues[i] = (byte)((alpha0 * (8 - i) + alpha1 * (i - 1)) / 7);
            }
            else
            {
                for (int i = 2; i < 6; i++)
                    alphaValues[i] = (byte)((alpha0 * (6 - i) + alpha1 * (i - 1)) / 5);
                alphaValues[6] = 0;
                alphaValues[7] = 255;
            }

            // Color block (8 bytes at offset + 8)
            ushort c0, c1;
            uint bits;

            // DXT data appears to be stored in little-endian format even on PS3
            c0 = (ushort)(data[offset + 8] | (data[offset + 9] << 8));
            c1 = (ushort)(data[offset + 10] | (data[offset + 11] << 8));
            bits = (uint)(data[offset + 12] | (data[offset + 13] << 8) |
                          (data[offset + 14] << 16) | (data[offset + 15] << 24));

            Color[] colors = new Color[4];
            colors[0] = RGB565ToColor(c0);
            colors[1] = RGB565ToColor(c1);
            colors[2] = InterpolateColor(colors[0], colors[1], 2, 1);
            colors[3] = InterpolateColor(colors[0], colors[1], 1, 2);

            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int destX = blockX + px;
                    int destY = blockY + py;
                    if (destX < width && destY < height)
                    {
                        int pixelIdx = py * 4 + px;
                        int colorIdx = (int)((bits >> (pixelIdx * 2)) & 0x3);
                        int alphaIdx = (int)((alphaBits >> (pixelIdx * 3)) & 0x7);

                        Color c = colors[colorIdx];
                        bitmap.SetPixel(destX, destY, Color.FromArgb(alphaValues[alphaIdx], c.R, c.G, c.B));
                    }
                }
            }
        }

        private Color RGB565ToColor(ushort rgb565)
        {
            int r = ((rgb565 >> 11) & 0x1F) * 255 / 31;
            int g = ((rgb565 >> 5) & 0x3F) * 255 / 63;
            int b = (rgb565 & 0x1F) * 255 / 31;
            return Color.FromArgb(255, r, g, b);
        }

        private Color InterpolateColor(Color c0, Color c1, int w0, int w1)
        {
            int total = w0 + w1;
            return Color.FromArgb(255,
                (c0.R * w0 + c1.R * w1) / total,
                (c0.G * w0 + c1.G * w1) / total,
                (c0.B * w0 + c1.B * w1) / total);
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
