using System.Windows.Forms;

namespace FastFileLib.WinForms
{
    /// <summary>
    /// Standalone window hosting a <see cref="LogsTabPage"/>. Use this from the smaller GUIs
    /// (Compiler, Tool, Converter) that don't have room for an embedded tab. Open it non-modally
    /// so the user can keep working while watching the log:
    /// <code>
    /// new LogViewerForm().Show(this);
    /// </code>
    /// Multiple instances are safe - all subscribe to the same shared LogService.
    /// </summary>
    public class LogViewerForm : Form
    {
        public LogViewerForm()
        {
            Text = "Logs";
            Width = 1100;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(700, 350);

            var content = new LogsTabPage
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(content);
        }
    }
}
