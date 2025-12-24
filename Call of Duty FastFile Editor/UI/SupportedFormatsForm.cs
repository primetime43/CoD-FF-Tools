using System;
using System.Drawing;
using System.Windows.Forms;

namespace Call_of_Duty_FastFile_Editor.UI
{
    public class SupportedFormatsForm : Form
    {
        private TabControl tabControl;
        private RichTextBox infoTextBox;

        public SupportedFormatsForm()
        {
            InitializeComponent();
            PopulateContent();
        }

        private void InitializeComponent()
        {
            this.Text = "Supported Formats & Features";
            this.Size = new Size(650, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Overview tab
            var overviewTab = new TabPage("Overview");
            var overviewText = CreateRichTextBox();
            overviewTab.Controls.Add(overviewText);
            tabControl.TabPages.Add(overviewTab);

            // Close button
            var closeButton = new Button
            {
                Text = "Close",
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            closeButton.Click += (s, e) => Close();

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45
            };
            closeButton.Location = new Point(buttonPanel.Width - 95, 8);
            buttonPanel.Controls.Add(closeButton);
            buttonPanel.Resize += (s, e) => closeButton.Location = new Point(buttonPanel.Width - 95, 8);

            this.Controls.Add(tabControl);
            this.Controls.Add(buttonPanel);
        }

        private RichTextBox CreateRichTextBox()
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None
            };
        }

        private void PopulateContent()
        {
            // Overview tab
            var overviewText = (RichTextBox)tabControl.TabPages[0].Controls[0];
            overviewText.Text = @"CALL OF DUTY FASTFILE EDITOR

This tool allows you to view, extract, and modify FastFile (.ff) archives from older Call of Duty games on various platforms.

QUICK REFERENCE
---------------
Full Support (Extract, Parse Assets, Edit, Repack):
  - CoD4: Modern Warfare - PS3, Xbox 360
  - CoD5: World at War - PS3, Xbox 360

Extract Only (Zone extraction, no asset parsing):
  - CoD4: Modern Warfare - PC, Wii
  - CoD5: World at War - PC, Wii
  - CoD6: Modern Warfare 2 - PS3, Xbox 360, PC

LIMITATIONS
-----------
- Xbox 360 files require a patched XEX to load modified FastFiles
- Signed FastFiles cannot be re-signed (use unsigned or patch console)
- Some asset types are view-only (images, models, etc.)
- PC and Wii versions have different zone structures";
        }
    }
}
