using System.IO;
using System.Runtime.InteropServices;
using Be.Windows.Forms;
using Call_of_Duty_FastFile_Editor.Constants;
using ICSharpCode.TextEditor; // TextEditorControlEx lives here (in the TextEditorEx assembly)

namespace Call_of_Duty_FastFile_Editor.UI
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    /// <summary>
    /// Central light/dark theming for the whole application.
    ///
    /// Forms are themed automatically: <see cref="Initialize"/> hooks
    /// <see cref="Application.Idle"/> and themes any newly-opened form (including
    /// modal dialogs) the moment it appears, so individual forms don't need to opt
    /// in. Call <see cref="SetTheme"/> from the View &gt; Dark Mode toggle to switch
    /// at runtime; the choice is persisted under %APPDATA%.
    /// </summary>
    public static class ThemeManager
    {
        public static AppTheme Current { get; private set; } = AppTheme.Light;
        public static bool IsDark => Current == AppTheme.Dark;

        /// <summary>Raised after the active theme changes (forms already re-themed).</summary>
        public static event Action? ThemeChanged;

        // Dark palette (VS-ish).
        private static readonly Color DarkWindow = Color.FromArgb(30, 30, 30);
        private static readonly Color DarkSurface = Color.FromArgb(37, 37, 38);
        private static readonly Color DarkControl = Color.FromArgb(45, 45, 48);
        private static readonly Color DarkBorder = Color.FromArgb(63, 63, 70);
        private static readonly Color DarkText = Color.FromArgb(241, 241, 241);
        private static readonly Color DarkTextDim = Color.FromArgb(153, 153, 153);
        private static readonly Color DarkSelection = Color.FromArgb(9, 71, 113);
        private static readonly Color DarkSelectionText = Color.White;

        // Forms we've already wired FormClosed cleanup for (so Idle themes each once).
        private static readonly HashSet<Form> _seenForms = new();
        // Controls whose owner-draw event handlers are already attached.
        private static readonly HashSet<Control> _wired = new();

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            Current = LoadPersistedTheme();
            ApplyToolStripRenderer();
            Application.Idle += OnApplicationIdle;
        }

        /// <summary>Switches the active theme, re-themes every open form, and persists the choice.</summary>
        public static void SetTheme(AppTheme theme)
        {
            if (Current == theme) return;
            Current = theme;
            ApplyToolStripRenderer();
            PersistTheme(theme);

            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
            {
                ApplyTheme(form);
                form.Refresh();
            }

            ThemeChanged?.Invoke();
        }

        public static void Toggle() => SetTheme(IsDark ? AppTheme.Light : AppTheme.Dark);

        private static void OnApplicationIdle(object? sender, EventArgs e)
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
            {
                if (_seenForms.Contains(form)) continue;
                _seenForms.Add(form);
                form.FormClosed += (_, _) => { _seenForms.Remove(form); };
                ApplyTheme(form);
            }
        }

        /// <summary>Applies the current theme to a control and all of its descendants.</summary>
        public static void ApplyTheme(Control root)
        {
            if (root == null) return;

            if (root is Form form)
                UseImmersiveDarkTitleBar(form, IsDark);

            ApplyToControl(root);
        }

        private static void ApplyToControl(Control control)
        {
            bool dark = IsDark;

            switch (control)
            {
                case TextEditorControlEx editor:
                    ThemeTextEditor(editor, dark);
                    return; // don't recurse into the editor's internal child controls

                case HexBox hex:
                    hex.BackColor = dark ? DarkControl : SystemColors.Window;
                    hex.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    return;

                case MenuStrip menu:
                    menu.BackColor = dark ? DarkSurface : SystemColors.Control;
                    menu.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;

                case StatusStrip status:
                    status.BackColor = dark ? DarkSurface : SystemColors.Control;
                    status.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;

                case ListView listView:
                    ThemeListView(listView, dark);
                    break;

                case TreeView tree:
                    tree.BackColor = dark ? DarkControl : SystemColors.Window;
                    tree.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    tree.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case TabControl tab:
                    ThemeTabControl(tab, dark);
                    break;

                case DataGridView grid:
                    ThemeDataGridView(grid, dark);
                    break;

                case Button button:
                    button.BackColor = dark ? DarkControl : SystemColors.Control;
                    button.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    button.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    button.FlatAppearance.BorderColor = dark ? DarkBorder : SystemColors.ControlDark;
                    break;

                case LinkLabel link: // must precede Label (LinkLabel : Label)
                    link.BackColor = Color.Transparent;
                    link.LinkColor = dark ? Color.FromArgb(86, 156, 214) : SystemColors.HotTrack;
                    link.ActiveLinkColor = dark ? Color.FromArgb(120, 180, 230) : SystemColors.HotTrack;
                    break;

                case CheckBox:
                case RadioButton:
                case Label:
                    control.BackColor = Color.Transparent;
                    control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;

                case TextBoxBase:
                case ComboBox:
                case NumericUpDown:
                case ListBox:
                    control.BackColor = dark ? DarkControl : SystemColors.Window;
                    control.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    break;

                case SplitContainer split:
                    split.BackColor = dark ? DarkWindow : SystemColors.Control;
                    split.Panel1.BackColor = dark ? DarkWindow : SystemColors.Control;
                    split.Panel2.BackColor = dark ? DarkWindow : SystemColors.Control;
                    break;

                case Panel: // also covers TabPage (TabPage : Panel)
                case GroupBox:
                case UserControl:
                case Form:
                    control.BackColor = dark ? DarkWindow : SystemColors.Control;
                    control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;

                default:
                    control.BackColor = dark ? DarkWindow : SystemColors.Control;
                    control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;
            }

            // Theme any attached context menu's items text via the manager renderer; the
            // renderer (ToolStripManager.Renderer) handles backgrounds globally.
            foreach (Control child in control.Controls)
                ApplyToControl(child);
        }

        #region ListView (dark column headers via owner-draw)

        private static void ThemeListView(ListView listView, bool dark)
        {
            listView.BackColor = dark ? DarkControl : SystemColors.Window;
            listView.ForeColor = dark ? DarkText : SystemColors.WindowText;
            listView.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;

            if (_wired.Add(listView))
            {
                listView.DrawColumnHeader += ListView_DrawColumnHeader;
                listView.DrawItem += (s, e) =>
                {
                    if (((ListView)s!).View != View.Details) e.DrawDefault = true;
                };
                listView.DrawSubItem += (s, e) => e.DrawDefault = true;
            }

            // Owner-draw only in dark mode (to recolor headers); default rendering in light.
            listView.OwnerDraw = dark;
        }

        private static void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var back = new SolidBrush(DarkSurface);
            e.Graphics.FillRectangle(back, e.Bounds);
            using (var pen = new Pen(DarkBorder))
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
            var textBounds = Rectangle.Inflate(e.Bounds, -4, 0);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? string.Empty, e.Font, textBounds, DarkText, flags);
        }

        #endregion

        #region TabControl (dark tab buttons via owner-draw)

        private static void ThemeTabControl(TabControl tab, bool dark)
        {
            tab.BackColor = dark ? DarkWindow : SystemColors.Control;

            if (_wired.Add(tab))
                tab.DrawItem += TabControl_DrawItem;

            tab.DrawMode = dark ? TabDrawMode.OwnerDrawFixed : TabDrawMode.Normal;
        }

        private static void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tab = (TabControl)sender!;
            var page = tab.TabPages[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var back = new SolidBrush(selected ? DarkControl : DarkSurface))
                e.Graphics.FillRectangle(back, e.Bounds);

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, page.Text, e.Font, e.Bounds,
                selected ? DarkText : DarkTextDim, flags);
        }

        #endregion

        #region DataGridView

        private static void ThemeDataGridView(DataGridView grid, bool dark)
        {
            if (dark)
            {
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = DarkWindow;
                grid.GridColor = DarkBorder;
                grid.BorderStyle = BorderStyle.FixedSingle;

                grid.DefaultCellStyle.BackColor = DarkControl;
                grid.DefaultCellStyle.ForeColor = DarkText;
                grid.DefaultCellStyle.SelectionBackColor = DarkSelection;
                grid.DefaultCellStyle.SelectionForeColor = DarkSelectionText;

                grid.ColumnHeadersDefaultCellStyle.BackColor = DarkSurface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkText;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkSurface;
                grid.RowHeadersDefaultCellStyle.BackColor = DarkSurface;
                grid.RowHeadersDefaultCellStyle.ForeColor = DarkText;
                grid.RowHeadersDefaultCellStyle.SelectionBackColor = DarkSelection;
            }
            else
            {
                grid.EnableHeadersVisualStyles = true;
                grid.BackgroundColor = SystemColors.AppWorkspace;
                grid.GridColor = SystemColors.ControlDark;
                grid.BorderStyle = BorderStyle.FixedSingle;
                grid.DefaultCellStyle.BackColor = SystemColors.Window;
                grid.DefaultCellStyle.ForeColor = SystemColors.WindowText;
                grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
                grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
                grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
                grid.RowHeadersDefaultCellStyle.BackColor = SystemColors.Control;
                grid.RowHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            }
        }

        #endregion

        #region ICSharpCode text editor

        // The editor's highlighting types (HighlightColor, DefaultHighlightingStrategy)
        // exist in BOTH referenced editor assemblies, so naming them directly is an
        // ambiguous reference. Drive the color overrides via reflection to stay decoupled.
        private static void ThemeTextEditor(TextEditorControlEx editor, bool dark)
        {
            editor.BackColor = dark ? DarkControl : SystemColors.Window;

            try
            {
                object? doc = editor.GetType().GetProperty("Document")?.GetValue(editor);
                object? strat = doc?.GetType().GetProperty("HighlightingStrategy")?.GetValue(doc);
                if (strat != null)
                {
                    Type? hcType = strat.GetType().Assembly
                        .GetType("ICSharpCode.TextEditor.Document.HighlightColor");
                    var setColorFor = hcType == null
                        ? null
                        : strat.GetType().GetMethod("SetColorFor", new[] { typeof(string), hcType });

                    if (hcType != null && setColorFor != null)
                    {
                        void Set(string name, Color fore, Color back) => setColorFor.Invoke(
                            strat, new object[] { name, Activator.CreateInstance(hcType, fore, back, false, false)! });

                        if (dark)
                        {
                            Set("Default", DarkText, DarkControl);
                            Set("LineNumbers", DarkTextDim, DarkSurface);
                            Set("CaretMarker", Color.FromArgb(50, 50, 53), Color.FromArgb(50, 50, 53));
                            Set("Selection", DarkSelectionText, DarkSelection);
                            Set("VRuler", DarkBorder, DarkControl);
                            Set("FoldLine", DarkTextDim, DarkSurface);
                            Set("FoldMarker", DarkText, DarkSurface);
                            Set("EOLMarkers", DarkBorder, DarkControl);
                            Set("SpaceMarkers", DarkBorder, DarkControl);
                            Set("TabMarkers", DarkBorder, DarkControl);
                        }
                        else
                        {
                            Color win = SystemColors.Window;
                            Color gutter = Color.FromArgb(224, 224, 224);
                            Set("Default", SystemColors.WindowText, win);
                            Set("LineNumbers", Color.Gray, win);
                            Set("CaretMarker", Color.Yellow, Color.Yellow);
                            Set("Selection", Color.White, SystemColors.Highlight);
                            Set("VRuler", gutter, win);
                            Set("FoldLine", Color.FromArgb(128, 128, 128), Color.White);
                            Set("FoldMarker", Color.FromArgb(50, 50, 50), Color.White);
                            Set("EOLMarkers", gutter, win);
                            Set("SpaceMarkers", gutter, win);
                            Set("TabMarkers", gutter, win);
                        }
                    }
                }
            }
            catch { /* highlighting theming is best-effort */ }

            editor.Refresh();
        }

        #endregion

        #region ToolStrip / menu renderer

        private static void ApplyToolStripRenderer()
        {
            ToolStripManager.Renderer = IsDark
                ? new ThemedToolStripRenderer(new DarkColorTable())
                : new ToolStripProfessionalRenderer();
        }

        private sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
        {
            public ThemedToolStripRenderer(ProfessionalColorTable table) : base(table) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                // Respect an explicitly-assigned color (e.g. the red "over max size"
                // status label); otherwise use the theme's text color.
                Color fore = e.Item.ForeColor;
                bool explicitColor = fore != Color.Empty
                    && fore != SystemColors.ControlText
                    && fore != DarkText;

                if (!explicitColor)
                    e.TextColor = e.Item.Enabled ? DarkText : DarkTextDim;

                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = DarkText;
                base.OnRenderArrow(e);
            }
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => DarkSurface;
            public override Color MenuStripGradientEnd => DarkSurface;
            public override Color ToolStripGradientBegin => DarkSurface;
            public override Color ToolStripGradientMiddle => DarkSurface;
            public override Color ToolStripGradientEnd => DarkSurface;
            public override Color StatusStripGradientBegin => DarkSurface;
            public override Color StatusStripGradientEnd => DarkSurface;
            public override Color ToolStripDropDownBackground => DarkControl;
            public override Color ImageMarginGradientBegin => DarkControl;
            public override Color ImageMarginGradientMiddle => DarkControl;
            public override Color ImageMarginGradientEnd => DarkControl;
            public override Color MenuBorder => DarkBorder;
            public override Color MenuItemBorder => DarkSelection;
            public override Color MenuItemSelected => DarkSelection;
            public override Color MenuItemSelectedGradientBegin => DarkSelection;
            public override Color MenuItemSelectedGradientEnd => DarkSelection;
            public override Color MenuItemPressedGradientBegin => DarkControl;
            public override Color MenuItemPressedGradientMiddle => DarkControl;
            public override Color MenuItemPressedGradientEnd => DarkControl;
            public override Color SeparatorDark => DarkBorder;
            public override Color SeparatorLight => DarkBorder;
            public override Color ButtonSelectedGradientBegin => DarkSelection;
            public override Color ButtonSelectedGradientMiddle => DarkSelection;
            public override Color ButtonSelectedGradientEnd => DarkSelection;
            public override Color ButtonPressedGradientBegin => DarkControl;
            public override Color ButtonPressedGradientMiddle => DarkControl;
            public override Color ButtonPressedGradientEnd => DarkControl;
        }

        #endregion

        #region Win32 dark title bar

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Windows 10 1809–1903
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;     // Windows 10 2004+

        private static void UseImmersiveDarkTitleBar(Form form, bool dark)
        {
            if (!form.IsHandleCreated)
            {
                // Defer until the handle exists, otherwise the attribute is lost.
                form.HandleCreated += (_, _) => UseImmersiveDarkTitleBar(form, IsDark);
                return;
            }

            int value = dark ? 1 : 0;
            if (DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, sizeof(int));
        }

        #endregion

        #region Persistence

        private static string SettingsFilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ApplicationConstants.ProgramName);
                return Path.Combine(dir, "theme.txt");
            }
        }

        private static AppTheme LoadPersistedTheme()
        {
            try
            {
                string path = SettingsFilePath;
                if (File.Exists(path) &&
                    string.Equals(File.ReadAllText(path).Trim(), "Dark", StringComparison.OrdinalIgnoreCase))
                    return AppTheme.Dark;
            }
            catch { /* fall back to light */ }
            return AppTheme.Light;
        }

        private static void PersistTheme(AppTheme theme)
        {
            try
            {
                string path = SettingsFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, theme == AppTheme.Dark ? "Dark" : "Light");
            }
            catch { /* non-fatal */ }
        }

        #endregion
    }
}
