using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiskCleaner.Core;

namespace DiskCleaner.Gui
{
    public class MainForm : Form
    {
        private readonly CleanerEngine _engine = new();
        private List<CleanCategory> _categories = new();

        private readonly ListView _listView;
        private readonly Button _btnScan;
        private readonly Button _btnClean;
        private readonly Button _btnDism;
        private readonly Button _btnRunAsAdmin;
        private readonly ProgressBar _progressBar;
        private readonly TextBox _logBox;
        private readonly Label _statusLabel;
        private readonly Label _totalLabel;

        public MainForm()
        {
            Text = "DiskCleaner";
            Width = 780;
            Height = 640;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new System.Drawing.Size(600, 480);

            // --- статус лента горе ---
            _statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };

            _btnRunAsAdmin = new Button
            {
                Text = "Рестартирай като администратор",
                Dock = DockStyle.Top,
                Height = 32,
                Visible = !_engine.IsAdmin,
            };
            _btnRunAsAdmin.Click += BtnRunAsAdmin_Click;

            // --- списък с категории ---
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                GridLines = true,
            };
            _listView.Columns.Add("Категория", 320);
            _listView.Columns.Add("Файлове", 80, HorizontalAlignment.Right);
            _listView.Columns.Add("Размер", 100, HorizontalAlignment.Right);
            _listView.Columns.Add("Статус", 160);
            _listView.ItemCheck += ListView_ItemCheck;

            // --- бутони за действие ---
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 6, 8, 6),
            };

            _btnScan = new Button { Text = "Сканирай", Width = 120, Height = 30 };
            _btnScan.Click += BtnScan_Click;

            _btnClean = new Button { Text = "Изчисти избраните", Width = 150, Height = 30, Enabled = false };
            _btnClean.Click += BtnClean_Click;

            _btnDism = new Button
            {
                Text = "Дълбоко чистене (DISM)",
                Width = 190,
                Height = 30,
                Enabled = _engine.IsAdmin,
            };
            _btnDism.Click += BtnDism_Click;

            buttonPanel.Controls.Add(_btnScan);
            buttonPanel.Controls.Add(_btnClean);
            buttonPanel.Controls.Add(_btnDism);

            // --- прогрес бар ---
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 20,
                Minimum = 0,
                Maximum = 100,
            };

            // --- лог отдолу ---
            _totalLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };

            _logBox = new TextBox
            {
                Dock = DockStyle.Bottom,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Height = 140,
                Font = new System.Drawing.Font("Consolas", 9f),
            };

            // ред на добавяне определя подредбата при Dock
            Controls.Add(_listView);
            Controls.Add(_logBox);
            Controls.Add(_totalLabel);
            Controls.Add(_progressBar);
            Controls.Add(buttonPanel);
            Controls.Add(_btnRunAsAdmin);
            Controls.Add(_statusLabel);

            _engine.Log += OnEngineLog;
            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            _statusLabel.Text = _engine.IsAdmin
                ? "Стартирано с администраторски права - пълно почистване достъпно."
                : "Стартирано без администраторски права - системните категории (Temp на Windows, Update кеш, DISM) ще бъдат пропуснати.";
        }

        private void OnEngineLog(string message)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.BeginInvoke(new Action(() => AppendLog(message)));
            }
            else
            {
                AppendLog(message);
            }
        }

        private void AppendLog(string message)
        {
            _logBox.AppendText(message + Environment.NewLine);
        }

        private void BtnRunAsAdmin_Click(object? sender, EventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas",
                };
                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Неуспешно рестартиране като администратор:\n{ex.Message}",
                    "Грешка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void BtnScan_Click(object? sender, EventArgs e)
        {
            SetBusy(true, "Сканиране...");
            _logBox.Clear();
            _totalLabel.Text = "";

            _categories = _engine.BuildCategories();
            _listView.Items.Clear();

            _progressBar.Maximum = Math.Max(_categories.Count, 1);
            _progressBar.Value = 0;

            await Task.Run(() =>
            {
                foreach (var cat in _categories)
                {
                    _engine.Scan(cat);
                    BeginInvoke(new Action(() => _progressBar.Value = Math.Min(_progressBar.Value + 1, _progressBar.Maximum)));
                }
            });

            PopulateListView();

            long total = _categories.Where(c => !c.IsRecycleBin).Sum(c => c.ScannedBytes);
            _totalLabel.Text = total == 0
                ? "Няма какво да се чисти - компютърът вече е чист."
                : $"Общо за почистване: {CleanerEngine.FormatSize(total)}";

            _btnClean.Enabled = total > 0 || _categories.Any(c => c.IsRecycleBin);
            SetBusy(false, null);
        }

        private void PopulateListView()
        {
            _listView.Items.Clear();
            foreach (var cat in _categories)
            {
                string filesText = cat.IsRecycleBin ? "?" : cat.ScannedFiles.ToString();
                string sizeText = cat.IsRecycleBin ? "(изпразва се)" : CleanerEngine.FormatSize(cat.ScannedBytes);
                string status = "";
                bool selectable = true;

                if (cat.RequiresAdmin && !_engine.IsAdmin)
                {
                    status = "нужни admin права";
                    filesText = "-";
                    sizeText = "-";
                    selectable = false;
                }
                else if (!cat.IsRecycleBin && cat.ScannedBytes == 0)
                {
                    status = "празно";
                    selectable = false;
                }

                var item = new ListViewItem(cat.Name) { Tag = cat };
                item.SubItems.Add(filesText);
                item.SubItems.Add(sizeText);
                item.SubItems.Add(status);
                item.Checked = selectable;
                item.ForeColor = selectable ? System.Drawing.SystemColors.WindowText : System.Drawing.SystemColors.GrayText;
                _listView.Items.Add(item);
            }
        }

        private void ListView_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            var item = _listView.Items[e.Index];
            if (item.Tag is CleanCategory cat)
            {
                bool selectable = cat.IsRecycleBin || (!(cat.RequiresAdmin && !_engine.IsAdmin) && cat.ScannedBytes > 0);
                if (!selectable)
                    e.NewValue = CheckState.Unchecked;
            }
        }

        private async void BtnClean_Click(object? sender, EventArgs e)
        {
            var selected = _listView.Items.Cast<ListViewItem>()
                .Where(i => i.Checked && i.Tag is CleanCategory)
                .Select(i => (CleanCategory)i.Tag!)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Няма избрани категории за чистене.", "DiskCleaner",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long totalToFree = selected.Where(c => !c.IsRecycleBin).Sum(c => c.ScannedBytes);
            var confirm = MessageBox.Show(this,
                $"Да изтрия избраните файлове и да освободя ~{CleanerEngine.FormatSize(totalToFree)}?",
                "Потвърждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Изтриване...");
            _progressBar.Maximum = Math.Max(selected.Count, 1);
            _progressBar.Value = 0;

            await Task.Run(() =>
            {
                foreach (var cat in selected)
                {
                    _engine.Clean(cat);
                    BeginInvoke(new Action(() => _progressBar.Value = Math.Min(_progressBar.Value + 1, _progressBar.Maximum)));
                }
            });

            PopulateResultsInListView(selected);

            long freed = selected.Sum(c => c.FreedBytes);
            int errors = selected.Sum(c => c.Errors);
            _totalLabel.Text = $"Освободено пространство: {CleanerEngine.FormatSize(freed)}" +
                                (errors > 0 ? $"  ({errors} файла пропуснати - вероятно се ползват от друга програма)" : "");

            SetBusy(false, null);
        }

        private void PopulateResultsInListView(List<CleanCategory> cleaned)
        {
            foreach (ListViewItem item in _listView.Items)
            {
                if (item.Tag is CleanCategory cat && cleaned.Contains(cat))
                {
                    item.SubItems[3].Text = cat.IsRecycleBin
                        ? "изпразнено"
                        : $"освободени {CleanerEngine.FormatSize(cat.FreedBytes)}" +
                          (cat.Errors > 0 ? $", {cat.Errors} пропуснати" : "");
                    item.Checked = false;
                }
            }
        }

        private async void BtnDism_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(this,
                "Ще стартирам DISM /StartComponentCleanup - маха стари версии на системни " +
                "компоненти (WinSxS). Обикновено освобождава 1-5 GB допълнително.\n\n" +
                "Отнема няколко минути и не може да се отмени. Продължавам ли?",
                "Дълбоко чистене", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Стартирам DISM (може да отнеме няколко минути)...");
            var result = await Task.Run(() => _engine.RunDismCleanup());
            SetBusy(false, null);

            if (!result.Started)
                MessageBox.Show(this, $"Неуспешно стартиране на DISM:\n{result.Error}", "Грешка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                MessageBox.Show(this,
                    result.ExitCode == 0
                        ? "DISM почистването приключи успешно."
                        : $"DISM приключи с код {result.ExitCode}.",
                    "DISM", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetBusy(bool busy, string? statusMessage)
        {
            _btnScan.Enabled = !busy;
            _btnClean.Enabled = !busy && _listView.Items.Count > 0;
            _btnDism.Enabled = !busy && _engine.IsAdmin;
            UseWaitCursor = busy;
            if (statusMessage != null)
                AppendLog(statusMessage);
        }
    }
}
