using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Visual preview of a parsed <see cref="MenuDef"/>: draws the 640×480 virtual screen and each
    /// item as a positioned box (from its window rect), colored by item type. Clicking an item box
    /// selects it and raises <see cref="SelectionChanged"/> so the host can show the item's properties.
    /// Read-only — a layout view of the structure the IW4 reader parsed.
    /// </summary>
    public sealed class MenuPreviewControl : Panel
    {
        // MW2 UI virtual resolution. Menu/item rects are authored in this space.
        private const float VirtualW = 640f;
        private const float VirtualH = 480f;

        private MenuDef? _menu;
        private readonly List<(ItemDef Item, RectangleF Bounds)> _hitBoxes = new();

        /// <summary>Raised when the selected item changes (null = the menu itself / empty area).</summary>
        public event EventHandler? SelectionChanged;

        public MenuPreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(32, 32, 36);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public ItemDef? SelectedItem { get; private set; }

        public MenuDef? Menu
        {
            get => _menu;
            set
            {
                _menu = value;
                SelectedItem = null;
                Invalidate();
            }
        }

        // Type → fill color, so the layout reads at a glance.
        private static Color TypeColor(int type) => (ItemType)type switch
        {
            ItemType.Text => Color.FromArgb(70, 130, 220),
            ItemType.Button => Color.FromArgb(80, 170, 90),
            ItemType.OwnerDraw => Color.FromArgb(120, 120, 130),
            ItemType.ListBox => Color.FromArgb(200, 150, 60),
            ItemType.EditField or ItemType.NumericField or ItemType.DecimalField or ItemType.EmailField or ItemType.PasswordField => Color.FromArgb(170, 110, 190),
            ItemType.Multi or ItemType.Enum or ItemType.YesNo => Color.FromArgb(60, 170, 175),
            ItemType.Model or ItemType.MenuModel => Color.FromArgb(180, 90, 90),
            _ => Color.FromArgb(95, 95, 105),
        };

        // Resolve a windowDef rect to virtual 640×480 screen coords using its horz/vert alignment.
        // CoD menu alignment (observed): 4 = FULLSCREEN (fill the screen), 2 = CENTER (offset from
        // the 320/240 center), 3 = RIGHT/BOTTOM (offset from the far edge), 0/1 = LEFT/TOP (raw).
        // This is an approximation of the engine's coordinate system, good enough to read the layout.
        private static RectangleF ResolveRect(RectDef r)
        {
            float x, y, w = r.W, h = r.H;
            switch (r.HorzAlign)
            {
                case 4: x = 0; w = VirtualW; break;
                case 2: x = VirtualW / 2f + r.X; break;
                case 3: x = VirtualW + r.X; break;
                default: x = r.X; break;
            }
            switch (r.VertAlign)
            {
                case 4: y = 0; h = VirtualH; break;
                case 2: y = VirtualH / 2f + r.Y; break;
                case 3: y = VirtualH + r.Y; break;
                default: y = r.Y; break;
            }
            // Normalize negative width/height (some overscan items store mirrored sizes).
            if (w < 0) { x += w; w = -w; }
            if (h < 0) { y += h; h = -h; }
            return new RectangleF(x, y, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            _hitBoxes.Clear();

            if (_menu == null)
            {
                TextRenderer.DrawText(g, "Select a menu to preview its layout.", Font, ClientRectangle,
                    Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var items = _menu.Items ?? new List<ItemDef>();

            // The "game window" is 16:9 widescreen (how MW2 actually renders) — wider than the 640×480
            // 4:3 authoring space. The 4:3 area is the centered safe area; backgrounds overscan to fill
            // the wider window. Items are authored in 640×480 coords, so the 4:3 area maps 1:1.
            float gameW = VirtualH * 16f / 9f;                 // ≈ 853.3
            float gameLeft = (VirtualW - gameW) / 2f;          // ≈ -106.7 (overscan extends each side)
            var gameWindow = new RectangleF(gameLeft, 0, gameW, VirtualH);
            float gameArea = gameW * VirtualH;

            // Resolve every item once; classify backdrop/overscan items (≈ full game window in a
            // dimension, or ≥ half its area) so they don't dominate the fit — they're drawn as faint
            // outlines, keeping the window frame and the normal interactive items (buttons) prominent.
            var resolved = new List<(ItemDef Item, RectangleF VR, bool Bg)>();
            foreach (var item in items)
            {
                if (item.Window?.Rect is not RectDef rd) continue;
                var vr = ResolveRect(rd);
                if (vr.Width <= 0.5f || vr.Height <= 0.5f) { vr.Width = 18; vr.Height = 12; }
                bool oversized = vr.Width >= gameW * 0.97f
                    || vr.Height >= VirtualH * 0.97f
                    || vr.Width * vr.Height > gameArea * 0.5f;
                resolved.Add((item, vr, oversized));
            }

            // Fit the view to the union of the game window and all NON-background item rects, so
            // nothing is clipped away and nothing bleeds into the panel.
            var bounds = gameWindow;
            foreach (var (_, vr, bg) in resolved)
                if (!bg) bounds = RectangleF.Union(bounds, vr);
            bounds.Inflate(bounds.Width * 0.02f, bounds.Height * 0.04f);

            const float margin = 12f;
            float availW = Math.Max(1, Width - margin * 2);
            float availH = Math.Max(1, Height - margin * 2);
            float scale = Math.Min(availW / bounds.Width, availH / bounds.Height);
            float offX = margin + (availW - bounds.Width * scale) / 2f - bounds.X * scale;
            float offY = margin + (availH - bounds.Height * scale) / 2f - bounds.Y * scale;
            RectangleF Map(RectangleF v) => new(offX + v.X * scale, offY + v.Y * scale, v.Width * scale, v.Height * scale);

            // 16:9 game window frame.
            var window = Map(gameWindow);
            using (var bg = new SolidBrush(Color.FromArgb(20, 20, 24)))
                g.FillRectangle(bg, window);
            using (var pen = new Pen(Color.FromArgb(120, 120, 132)))
                g.DrawRectangle(pen, window.X, window.Y, window.Width, window.Height);
            TextRenderer.DrawText(g, "16:9 game window", Font,
                new Point((int)window.X + 4, (int)(window.Bottom - 18)), Color.FromArgb(120, 120, 132));

            // 640×480 4:3 safe-area reference (dotted), centered in the window.
            var screen = Map(new RectangleF(0, 0, VirtualW, VirtualH));
            using (var pen = new Pen(Color.FromArgb(80, 90, 110)) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pen, screen.X, screen.Y, screen.Width, screen.Height);
            TextRenderer.DrawText(g, "640 × 480 safe area", Font,
                new Point((int)screen.X + 4, (int)screen.Y + 2), Color.FromArgb(95, 105, 125));

            using var labelFont = new Font(Font.FontFamily, 7.5f);
            using var labelBrush = new SolidBrush(Color.White);
            using var labelFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center };

            foreach (var (item, vr, isBackground) in resolved)
            {
                var box = Map(vr);
                if (box.Width < 5) box.Width = 5;
                if (box.Height < 4) box.Height = 4;

                var color = TypeColor(item.Type);
                bool selected = ReferenceEquals(item, SelectedItem);
                _hitBoxes.Add((item, box));

                if (isBackground && !selected)
                {
                    using var pen = new Pen(Color.FromArgb(70, color)) { DashStyle = DashStyle.Dot };
                    g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                    continue; // outline only — keeps the canvas readable
                }

                using (var fill = new SolidBrush(Color.FromArgb(selected ? 165 : 110, color)))
                    g.FillRectangle(fill, box);
                using (var pen = new Pen(selected ? Color.White : Color.FromArgb(215, color), selected ? 2f : 1f))
                    g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

                string label = !string.IsNullOrEmpty(item.Text) ? item.Text
                    : !string.IsNullOrEmpty(item.Window?.Name) ? item.Window!.Name
                    : ((ItemType)item.Type).ToString();
                if (box.Width > 26 && box.Height > 10)
                {
                    // GDI+ DrawString (unlike TextRenderer) honors clipping and stays inside the box.
                    var textRect = new RectangleF(box.X + 2, box.Y, box.Width - 4, box.Height);
                    g.DrawString(label, labelFont, labelBrush, textRect, labelFormat);
                }
            }

            if (resolved.Count == 0)
            {
                TextRenderer.DrawText(g, "(no items)", Font, Rectangle.Round(screen),
                    Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            // Topmost item wins (later items draw on top).
            ItemDef? hit = null;
            for (int i = _hitBoxes.Count - 1; i >= 0; i--)
            {
                if (_hitBoxes[i].Bounds.Contains(e.Location)) { hit = _hitBoxes[i].Item; break; }
            }
            if (!ReferenceEquals(hit, SelectedItem))
            {
                SelectedItem = hit;
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
