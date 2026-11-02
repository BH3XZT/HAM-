using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace HamStartup
{
    public partial class MainForm
    {
        private ListView _monitorList;
        private Label _monitorSummary;
        private Label _monitorAlert;
        private TextBox _monitorDetail;
        private RuntimeInspector _inspector;
        private RuntimeSnapshot _monitorSnapshot;
        private CheckBox _chkMonitorEnabled;
        private CheckBox _chkAutoShowMonitor;
        private Button _monitorRefreshButton;
        private int _monitorTicks;

        private void BuildMonitorTab(Panel page)
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = UiTheme.Bg;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(root);

            root.Controls.Add(BuildMonitorBanner(), 0, 0);

            _monitorContentHost = new Panel();
            _monitorContentHost.Dock = DockStyle.Fill;
            _monitorContentHost.BackColor = UiTheme.Bg;
            _monitorClassicContent = BuildMonitorContent();
            _monitorClassicContent.Dock = DockStyle.Fill;
            _monitorCardView = BuildMonitorCardView();
            _monitorCardView.Visible = false;
            _monitorContentHost.Controls.Add(_monitorClassicContent);
            _monitorContentHost.Controls.Add(_monitorCardView);
            root.Controls.Add(_monitorContentHost, 0, 1);
        }

        private Control BuildMonitorBanner()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;

            Label title = UiTheme.MakeBoldLabel("运行监测 / 当前 Ham 软件", 12f, UiTheme.Text);
            title.Location = new Point(18, 10);
            card.Controls.Add(title);

            _monitorSummary = UiTheme.MakeLabel("正在检测…", 9f, UiTheme.TextMuted);
            _monitorSummary.Location = new Point(18, 36);
            card.Controls.Add(_monitorSummary);

            _monitorAlert = UiTheme.MakeLabel("", 8.8f, UiTheme.Red);
            _monitorAlert.Location = new Point(18, 60);
            card.Controls.Add(_monitorAlert);

            _monitorRefreshButton = UiTheme.SecondaryButton("立即检测", 96, 32);
            _monitorRefreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _monitorRefreshButton.Click += delegate
            {
                RefreshMonitor(true);
                AppendLog("已刷新运行监测。", LogKind.Info);
            };
            card.Controls.Add(_monitorRefreshButton);

            _chkMonitorEnabled = new CheckBox();
            _chkMonitorEnabled.Text = "启用监测";
            _chkMonitorEnabled.AutoSize = true;
            _chkMonitorEnabled.Font = UiTheme.Font(8.6f);
            _chkMonitorEnabled.ForeColor = UiTheme.TextMuted;
            _chkMonitorEnabled.BackColor = Color.White;
            _chkMonitorEnabled.Checked = _settings.MonitorEnabled;
            _chkMonitorEnabled.CheckedChanged += delegate
            {
                _settings.MonitorEnabled = _chkMonitorEnabled.Checked;
                TrySave();
                RefreshMonitor(true);
            };
            card.Controls.Add(_chkMonitorEnabled);

            _chkAutoShowMonitor = new CheckBox();
            _chkAutoShowMonitor.Text = "启动后自动打开";
            _chkAutoShowMonitor.AutoSize = true;
            _chkAutoShowMonitor.Font = UiTheme.Font(8.6f);
            _chkAutoShowMonitor.ForeColor = UiTheme.TextMuted;
            _chkAutoShowMonitor.BackColor = Color.White;
            _chkAutoShowMonitor.Checked = _settings.AutoShowMonitor;
            _chkAutoShowMonitor.CheckedChanged += delegate
            {
                _settings.AutoShowMonitor = _chkAutoShowMonitor.Checked;
                TrySave();
            };
            card.Controls.Add(_chkAutoShowMonitor);

            card.Resize += delegate
            {
                int right = card.ClientSize.Width - 18;
                _monitorRefreshButton.Location = new Point(right - _monitorRefreshButton.Width, 12);
                _chkAutoShowMonitor.Location = new Point(right - _chkAutoShowMonitor.Width, 58);
                _chkMonitorEnabled.Location = new Point(
                    _chkAutoShowMonitor.Left - _chkMonitorEnabled.Width - 14, 58);
                _monitorSummary.Width = Math.Max(300, right - 380);
                _monitorAlert.Width = Math.Max(300, right - 380);
            };
            return card;
        }

        private Control BuildMonitorContent()
        {
            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.BackColor = UiTheme.Bg;
            content.ColumnCount = 2;
            content.RowCount = 1;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _monitorList = new ListView();
            _monitorList.Dock = DockStyle.Fill;
            _monitorList.View = View.Details;
            _monitorList.FullRowSelect = true;
            _monitorList.HideSelection = false;
            _monitorList.MultiSelect = false;
            _monitorList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _monitorList.BorderStyle = BorderStyle.FixedSingle;
            _monitorList.BackColor = Color.White;
            _monitorList.ForeColor = UiTheme.Text;
            _monitorList.Font = UiTheme.Font(9f);
            _monitorList.ShowItemToolTips = true;
            _monitorList.Margin = new Padding(0, 0, 12, 0);

            ImageList rowHeight = new ImageList();
            rowHeight.ImageSize = new Size(1, 30);
            rowHeight.ColorDepth = ColorDepth.Depth32Bit;
            rowHeight.Images.Add(new Bitmap(1, 30));
            _monitorList.SmallImageList = rowHeight;

            _monitorList.Columns.Add("软件", 105, HorizontalAlignment.Left);
            _monitorList.Columns.Add("状态", 80, HorizontalAlignment.Left);
            _monitorList.Columns.Add("PID", 62, HorizontalAlignment.Left);
            _monitorList.Columns.Add("CPU", 55, HorizontalAlignment.Left);
            _monitorList.Columns.Add("内存", 66, HorizontalAlignment.Left);
            _monitorList.Columns.Add("UDP", 58, HorizontalAlignment.Left);
            _monitorList.Columns.Add("DX集群", 58, HorizontalAlignment.Left);
            _monitorList.Columns.Add("ADI 文件", 160, HorizontalAlignment.Left);
            _monitorList.SelectedIndexChanged += delegate { UpdateMonitorDetail(); };
            _monitorList.DoubleClick += delegate
            {
                RuntimeAppInfo app = SelectedMonitorApp();
                if (app != null && !string.IsNullOrEmpty(app.Path))
                    ProcessUtil.OpenFolder(app.Path);
            };

            content.Controls.Add(_monitorList, 0, 0);
            content.Controls.Add(BuildMonitorDetailCard(), 1, 0);
            return content;
        }

        private Control BuildMonitorDetailCard()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;

            Label title = UiTheme.MakeBoldLabel("检测详情", 11f, UiTheme.Text);
            title.Location = new Point(18, 12);
            card.Controls.Add(title);

            _monitorDetail = new TextBox();
            _monitorDetail.Multiline = true;
            _monitorDetail.Location = new Point(18, 40);
            _monitorDetail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _monitorDetail.ReadOnly = true;
            _monitorDetail.BorderStyle = BorderStyle.None;
            _monitorDetail.BackColor = Color.White;
            _monitorDetail.ForeColor = UiTheme.Text;
            _monitorDetail.Font = UiTheme.Font(8.8f);
            _monitorDetail.WordWrap = true;
            _monitorDetail.ScrollBars = ScrollBars.Vertical;
            _monitorDetail.Text = "在上方选择一项查看详情。";
            card.Controls.Add(_monitorDetail);

            card.Resize += delegate
            {
                _monitorDetail.Width = Math.Max(100, card.ClientSize.Width - 34);
                _monitorDetail.Height = Math.Max(80, card.ClientSize.Height - 54);
            };
            return card;
        }

        private RuntimeAppInfo SelectedMonitorApp()
        {
            if (_monitorList == null || _monitorList.SelectedItems.Count == 0) return null;
            return _monitorList.SelectedItems[0].Tag as RuntimeAppInfo;
        }

        private void RefreshMonitor(bool full)
        {
            if (_monitorList == null || _settings == null) return;
            if (!_settings.MonitorEnabled)
            {
                _monitorSummary.Text = "运行监测已关闭（可在右上角勾选“启用监测”）。";
                _monitorAlert.Text = "";
                return;
            }

            if (_inspector == null) _inspector = new RuntimeInspector();
            RuntimeSnapshot snapshot;
            try { snapshot = _inspector.Collect(_settings, full); }
            catch (Exception ex)
            {
                _monitorSummary.Text = "运行监测失败：" + ex.Message;
                return;
            }
            _monitorSnapshot = snapshot;
            try { UpdateLinkMatches(snapshot, true); }
            catch { }

            int selectedPid = -1;
            string selectedName = null;
            RuntimeAppInfo previous = SelectedMonitorApp();
            if (previous != null)
            {
                selectedPid = previous.Pid;
                selectedName = previous.Name;
            }

            _monitorList.BeginUpdate();
            _monitorList.Items.Clear();
            foreach (RuntimeAppInfo app in snapshot.Apps)
            {
                ListViewItem item = new ListViewItem(app.Name);
                item.SubItems.Add(app.Status);
                item.SubItems.Add(app.Running ? app.Pid.ToString() : "—");
                item.SubItems.Add(app.Running ? string.Format("{0:0}", app.CpuPercent) + "%" : "—");
                item.SubItems.Add(app.Running ? app.MemoryMb + " MB" : "—");
                item.SubItems.Add(app.UdpText);
                item.SubItems.Add(app.ClusterText);
                item.SubItems.Add(app.AdiText);
                item.Tag = app;
                item.ImageIndex = 0;
                item.ToolTipText = string.IsNullOrEmpty(app.AdiText) || app.AdiText == "—"
                    ? (app.Path ?? "")
                    : app.AdiText;

                if (app.Running && (app.Status.Contains("无响应") || app.Status.Contains("卡死")))
                {
                    item.ForeColor = UiTheme.Red;
                }
                else if (app.Running)
                {
                    item.ForeColor = UiTheme.Text;
                    item.SubItems[1].ForeColor = UiTheme.Green;
                }
                else
                {
                    item.ForeColor = UiTheme.TextFaint;
                }
                _monitorList.Items.Add(item);

                if ((selectedPid > 0 && app.Pid == selectedPid) ||
                    (selectedPid <= 0 && selectedName != null && app.Name == selectedName))
                {
                    item.Selected = true;
                }
            }
            _monitorList.EndUpdate();

            _monitorSummary.Text = string.Format(
                "发现 {0} 个运行中的 Ham 程序 · UDP 端点 {1} 个 · DX 集群连接 {2} 条 · ADI 文件 {3} 个" +
                (_linkMatchedCount > 0 ? " · 已匹配连接方案 {4} 个" : ""),
                snapshot.RunningCount, snapshot.UdpCount, snapshot.ClusterCount, snapshot.AdiFiles.Count,
                _linkMatchedCount);

            if (snapshot.NewAlerts.Count > 0)
            {
                foreach (string alert in snapshot.NewAlerts)
                {
                    AppendLog(alert, LogKind.Warn);
                    ShowTrayAlert("Ham 软件状态异常", alert);
                }
            }

            if (snapshot.RecentAlerts.Count > 0)
            {
                int from = Math.Max(0, snapshot.RecentAlerts.Count - 2);
                _monitorAlert.Text = string.Join("   ", snapshot.RecentAlerts.GetRange(from,
                    snapshot.RecentAlerts.Count - from).ToArray());
            }
            else
            {
                if (_linkMatchedCount > 0)
                    _monitorAlert.Text = "已自动匹配 " + _linkMatchedCount + " 个连接方案；可在“连接联动”页查看详情。";
                else
                    _monitorAlert.Text = snapshot.RunningCount == 0
                        ? "当前没有检测到正在运行的 Ham 软件。"
                        : "未发现异常；每 3 秒自动刷新。";
            }

            if (_monitorList.SelectedItems.Count == 0 && _monitorList.Items.Count > 0)
                _monitorList.Items[0].Selected = true;
            UpdateMonitorDetail();
            if (_monitorCardView != null) RefreshMonitorCards();
        }

        private void UpdateMonitorDetail()
        {
            RuntimeAppInfo app = SelectedMonitorApp();
            if (app == null)
            {
                _monitorDetail.Text = "在上方选择一项查看详情。";
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("软件：" + app.Name + (app.IsConfigured ? "（当前方案）" : "（自动发现）"));
            sb.AppendLine("进程：" + app.ProcessName + (app.Running ? "   PID " + app.Pid : ""));
            string statusSuffix = (!app.Responding && !app.Status.Contains("无响应")) ? "   [无响应]" : "";
            sb.AppendLine("状态：" + app.Status + statusSuffix);
            if (app.StartTime.HasValue) sb.AppendLine("启动：" + app.StartTime.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            if (app.Running)
                sb.AppendLine(string.Format("CPU：{0:0.0}%   内存：{1} MB", app.CpuPercent, app.MemoryMb));
            sb.AppendLine("路径：" + (string.IsNullOrEmpty(app.Path) ? "—" : app.Path));
            if (!string.IsNullOrEmpty(app.WindowTitle)) sb.AppendLine("窗口：" + app.WindowTitle);
            sb.AppendLine();
            sb.AppendLine("UDP 服务：");
            if (app.Udp.Count == 0) sb.AppendLine("  （未检测到 UDP 端点）");
            else foreach (RuntimeEndpoint ep in app.Udp) sb.AppendLine("  " + ep.ToString());
            sb.AppendLine();
            sb.AppendLine("TCP / DX 集群：");
            if (app.Tcp.Count == 0) sb.AppendLine("  （未检测到 TCP 连接）");
            else
            {
                int count = 0;
                foreach (TcpConnectionInfo c in app.Tcp)
                {
                    sb.AppendLine("  " + c.ToString());
                    if (++count >= 12)
                    {
                        sb.AppendLine("  …");
                        break;
                    }
                }
            }
            sb.AppendLine();
            sb.AppendLine("ADI 记录文件：");
            if (app.AdiFiles.Count == 0)
            {
                sb.AppendLine("  （未找到与该软件匹配的 ADI/ADIF 文件）");
                if (_monitorSnapshot != null && _monitorSnapshot.AdiFiles.Count > 0)
                {
                    sb.AppendLine("  最近检测到的 ADI 文件：");
                    int shown = 0;
                    foreach (AdiFileInfo file in _monitorSnapshot.AdiFiles)
                    {
                        sb.AppendLine("  " + file.Path);
                        if (++shown >= 3) break;
                    }
                }
            }
            else
            {
                foreach (AdiFileInfo file in app.AdiFiles)
                {
                    sb.AppendLine("  " + file.Path);
                    sb.AppendLine(string.Format("      {0:0.0} MB   {1:yyyy-MM-dd HH:mm}",
                        file.Size / 1024.0 / 1024.0, file.Modified));
                }
            }
            _monitorDetail.Text = sb.ToString();
        }

        private void MonitorTick()
        {
            if (_settings == null || !_settings.MonitorEnabled) return;
            _monitorTicks++;
            if (_monitorTicks % 2 == 0) RefreshMonitor(false);
        }

        private void StartupCompleted()
        {
            if (_settings.MonitorEnabled)
            {
                RefreshMonitor(true);
                if (_settings.AutoShowMonitor) SelectTab(2);
                if (_monitorSnapshot != null)
                {
                    AppendLog(string.Format("服务检测：运行 {0} 个 Ham 程序，UDP 端点 {1}，DX 集群 {2}，ADI 文件 {3}。",
                        _monitorSnapshot.RunningCount, _monitorSnapshot.UdpCount,
                        _monitorSnapshot.ClusterCount, _monitorSnapshot.AdiFiles.Count), LogKind.Info);
                    foreach (RuntimeAppInfo app in _monitorSnapshot.Apps)
                    {
                        if (app.Running && app.AdiFiles.Count > 0)
                            AppendLog("  ADI：" + app.Name + " → " + app.AdiFiles[0].Path, LogKind.Info);
                    }
                }
            }
        }

        private void ShowTrayAlert(string title, string message)
        {
            try
            {
                if (_tray == null)
                {
                    _tray = new NotifyIcon();
                    _tray.Icon = Icon;
                    _tray.Text = "HAM 工作台";
                    _tray.Visible = true;
                    _tray.DoubleClick += delegate
                    {
                        Show();
                        WindowState = FormWindowState.Normal;
                        Activate();
                    };
                }
                _tray.ShowBalloonTip(5000, title, message, ToolTipIcon.Warning);
            }
            catch { }
        }
    }
}
