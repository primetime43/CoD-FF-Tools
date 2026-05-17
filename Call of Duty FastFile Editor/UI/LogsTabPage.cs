using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastFileLib.Logging;

namespace Call_of_Duty_FastFile_Editor.UI
{
    /// <summary>
    /// Tab content showing live log entries from <see cref="LogService"/> with severity
    /// + category filters, clear, export, and auto-scroll controls. Subscribes to
    /// LogService events; safe to add/remove from the UI lifecycle.
    /// </summary>
    public partial class LogsTabPage : UserControl
    {
        private const string AllCategories = "(All)";

        // Static-color map per severity for visual scanning
        private static readonly Dictionary<LogSeverity, Color> SeverityColors = new()
        {
            { LogSeverity.Debug,   Color.Gray },
            { LogSeverity.Info,    Color.Black },
            { LogSeverity.Warning, Color.DarkOrange },
            { LogSeverity.Error,   Color.Firebrick },
        };

        // Categories we've already added to the dropdown, kept in a set so we don't add dupes
        private readonly HashSet<string> _knownCategories = new(StringComparer.OrdinalIgnoreCase);

        public LogsTabPage()
        {
            InitializeComponent();

            // Severity filter values: All + each level
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

            // Hydrate from any entries already captured before we attached
            RefreshAll();
        }

        // ---- Event handlers (may fire on any thread; marshal to UI) ----

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

        // ---- UI updates ----

        private void AddEntryToUi(LogEntry e)
        {
            // Track new categories
            if (!_knownCategories.Contains(e.Category))
            {
                _knownCategories.Add(e.Category);
                categoryFilter.Items.Add(e.Category);
            }

            // Apply current filter
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

                // Rebuild category dropdown to reflect known categories
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
            // Severity
            if (severityFilter.SelectedIndex > 0)
            {
                // SelectedIndex 0 == "All"; 1+ map to enum values in declaration order
                var minSeverity = (LogSeverity)(severityFilter.SelectedIndex - 1);
                if (e.Severity < minSeverity) return false;
            }

            // Category
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
