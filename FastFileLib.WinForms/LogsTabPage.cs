using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FastFileLib.Logging;

namespace FastFileLib.WinForms
{
    /// <summary>
    /// UserControl showing live log entries from <see cref="LogService"/> with severity and
    /// category filters, clear, export, and auto-scroll. Can be embedded directly in a tab
    /// or hosted in a <see cref="LogViewerForm"/> popup. Thread-safe - marshals LogService
    /// events to the UI thread.
    /// </summary>
    public partial class LogsTabPage : UserControl
    {
        private const string AllCategories = "(All)";

        private static readonly Dictionary<LogSeverity, Color> SeverityColors = new()
        {
            { LogSeverity.Debug,   Color.Gray },
            { LogSeverity.Info,    Color.Black },
            { LogSeverity.Warning, Color.DarkOrange },
            { LogSeverity.Error,   Color.Firebrick },
        };

        private readonly HashSet<string> _knownCategories = new(StringComparer.OrdinalIgnoreCase);

        public LogsTabPage()
        {
            InitializeComponent();

            severityFilter.Items.Add("All");
            foreach (var sev in Enum.GetValues<LogSeverity>())
                severityFilter.Items.Add(sev.ToString());
            severityFilter.SelectedIndex = 0;
            severityFilter.SelectedIndexChanged += (_, _) => RefreshAll();

            categoryFilter.Items.Add(AllCategories);
            categoryFilter.SelectedIndex = 0;
            categoryFilter.SelectedIndexChanged += (_, _) => RefreshAll();

            clearButton.Click += (_, _) =>
            {
                if (MessageBox.Show(this, "Clear all log entries from memory?", "Clear logs",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    LogService.Clear();
                }
            };
            exportButton.Click += (_, _) => ExportLog();

            LogService.EntryAdded += OnEntryAdded;
            LogService.Cleared    += OnCleared;

            RefreshAll();
        }

        private void OnEntryAdded(object? sender, LogEntry e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => AddEntryToUi(e))); } catch (ObjectDisposedException) { }
            }
            else
            {
                AddEntryToUi(e);
            }
        }

        private void OnCleared(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(new Action(RefreshAll)); } catch (ObjectDisposedException) { } }
            else { RefreshAll(); }
        }

        private void AddEntryToUi(LogEntry e)
        {
            if (!_knownCategories.Contains(e.Category))
            {
                _knownCategories.Add(e.Category);
                categoryFilter.Items.Add(e.Category);
            }

            if (!PassesFilter(e)) { UpdateCountLabel(); return; }

            var item = BuildListItem(e);
            logListView.Items.Add(item);
            UpdateCountLabel();

            if (autoScrollButton.Checked && logListView.Items.Count > 0)
                logListView.EnsureVisible(logListView.Items.Count - 1);
        }

        private void RefreshAll()
        {
            logListView.BeginUpdate();
            try
            {
                logListView.Items.Clear();

                var entries = LogService.GetAll();
                foreach (var e in entries)
                {
                    if (!_knownCategories.Contains(e.Category))
                    {
                        _knownCategories.Add(e.Category);
                        categoryFilter.Items.Add(e.Category);
                    }
                }

                foreach (var e in entries)
                {
                    if (PassesFilter(e))
                        logListView.Items.Add(BuildListItem(e));
                }
            }
            finally
            {
                logListView.EndUpdate();
            }
            UpdateCountLabel();
            if (autoScrollButton.Checked && logListView.Items.Count > 0)
                logListView.EnsureVisible(logListView.Items.Count - 1);
        }

        private ListViewItem BuildListItem(LogEntry e)
        {
            var item = new ListViewItem(new[]
            {
                e.Timestamp.ToString("HH:mm:ss.fff"),
                e.Severity.ToString(),
                e.Category,
                e.Exception == null ? e.Message
                                    : $"{e.Message}  ({e.Exception.GetType().Name}: {e.Exception.Message})"
            });
            item.ForeColor = SeverityColors[e.Severity];
            item.Tag = e;
            return item;
        }

        private bool PassesFilter(LogEntry e)
        {
            if (severityFilter.SelectedIndex > 0)
            {
                var minSeverity = (LogSeverity)(severityFilter.SelectedIndex - 1);
                if (e.Severity < minSeverity) return false;
            }

            string selectedCategory = categoryFilter.SelectedItem as string ?? AllCategories;
            if (selectedCategory != AllCategories &&
                !string.Equals(e.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void UpdateCountLabel()
        {
            int total = LogService.Count;
            int shown = logListView.Items.Count;
            countLabel.Text = shown == total ? $"{total} entries" : $"{shown} / {total} entries";
        }

        private void ExportLog()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Export log",
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"ffeditor-log-{DateTime.Now:yyyyMMdd-HHmmss}.log",
                AddExtension = true,
                DefaultExt = "log"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                int written = LogService.Export(dlg.FileName);
                MessageBox.Show(this, $"Exported {written} entries to:\n{dlg.FileName}",
                    "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogService.Error("LogsTab", $"Export failed: {ex.Message}", ex);
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
