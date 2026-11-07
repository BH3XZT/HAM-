using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HamStartup
{
    public partial class MainForm : Form
    {
        private readonly string _configPath;
        private readonly bool _autoStart;
        private readonly bool _quitAfterStart;

        private AppSettings _settings;
        private Launcher _launcher;
        private CancellationTokenSource _cts;
        private bool _running;
        private System.Windows.Forms.Timer _statusTimer;

        private StatusPill _pill;
        private Label _headerSaveHint;
        private Panel _tabBar;
        private Panel _tabContent;
        private Panel _tabStartPanel;
        private Panel _tabUdpPanel;
        private Panel _tabMonitorPanel;
        private Panel _tabTimePanel;
        private Button _tabStartButton;
        private Button _tabUdpButton;
        private Button _tabMonitorButton;
        private Button _tabTimeButton;
        private Panel _tabIndicator;
        private NotifyIcon _tray;
        private int _activeTab;
        private RichTextBox _log;
        private Label _runHint;
        private SplitContainer _mainSplit;
        private bool _mainSplitInitialized;
        private ToolTip _resizeTip;

        private static readonly object LogFileLock = new object();

        public MainForm(string configPath, bool autoStart, bool quitAfterStart)
        {
            _configPath = configPath;
            _autoStart = autoStart;
            _quitAfterStart = quitAfterStart;

            Text = "HAM 工作台 · 电台软件联动启动器";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1140, 800);
            MinimumSize = new Size(1040, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.Bg;
            Font = UiTheme.Font(9.5f);
            DoubleBuffered = true;

            LoadSettings();
            _launcher = new Launcher(_settings, OnLauncherLog);
            _resizeTip = new ToolTip();
            BuildShell();
            RefreshSchemeCombo();

            BuildAppList();
            FillUdpFields();
            RefreshUdpState(false);
            RefreshStatus();

            AppendLog("HAM 工作台已就绪，配置自动保存在：", LogKind.Info);
            AppendLog("  " + _configPath, LogKind.Info);
            try
            {
                if (_configPath.StartsWith(AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                    AppendLog("  使用便携模式：设置、日志、备份都保存在程序所在目录。", LogKind.Info);
                else if (_configPath.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
                    AppendLog("  警告：用户目录不可写，已临时使用系统临时目录保存设置；重启后可能丢失。", LogKind.Warn);
            }
            catch { }
            foreach (AppEntry app in _settings.Apps)
            {
                if (app.PathExists)
                {
                    AppendLog("  " + app.Name + "  →  " + app.ExePath, LogKind.Info);
                    if (!app.IsShortcut)
                    {
                        try
                        {
                            string lnk = ShortcutResolver.FindDesktopShortcut(
                                AppDetector.ShortcutKeyword(app.Id), AppCatalog.KnownExeName(app.Id));
                            if (!string.IsNullOrEmpty(lnk))
                                AppendLog("      也可点“自动检测”改用桌面图标：" + lnk, LogKind.Info);
                        }
                        catch { }
                    }
                }
                else
                {
                    AppendLog("  " + app.Name + "  →  (未设置路径)", LogKind.Warn);
                }
            }
            if (SecurityUtil.IsLowIntegrity())
            {
                AppendLog("警告：当前以受限权限（低完整性）运行，QLog 可能显示全新配置；", LogKind.Warn);
                AppendLog("      建议关闭程序，从资源管理器正常双击 HamStartup.exe，或选择以管理员身份重启。", LogKind.Warn);
            }
            if (_autoStart)
            {
                Shown += delegate { BeginInvoke(new Action(StartSequence)); };
            }
            Shown += delegate
            {
                BeginInvoke(new Action(delegate
                {
                    InitializeSplitLayout();
                    RefreshStatus();
                    RefreshMonitor(true);
                    if (_settings.AutoShowMonitor && _monitorSnapshot != null &&
                        _monitorSnapshot.RunningCount > 0)
                    {
                        SelectTab(2);
                    }
                }));
            };

            _statusTimer = new System.Windows.Forms.Timer();
            _statusTimer.Interval = 1500;
            _statusTimer.Tick += delegate { RefreshStatus(); MonitorTick(); };
            _statusTimer.Start();

            FormClosing += MainForm_FormClosing;
        }

        private Panel _header;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                int result = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
                if (result != 0) DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            }
            catch { }
        }

        public void ShowTab(int index)
        {
            SelectTab(index);
        }

        public void ScrollUdp(int y)
        {
            if (_udpScroll == null) return;
            _udpScroll.AutoScrollPosition = new Point(0, Math.Abs(y));
            _udpScroll.PerformLayout();
            Application.DoEvents();
            _udpScroll.AutoScrollPosition = new Point(0, Math.Abs(y));
            _udpScroll.Refresh();
            Application.DoEvents();
        }

        private Button MakeTabButton(string text, int x)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, 0);
            button.Size = new Size(126, 41);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.White;
            button.FlatAppearance.MouseDownBackColor = Color.White;
            button.BackColor = Color.White;
            button.ForeColor = UiTheme.TextMuted;
            button.Font = UiTheme.Font(10f);
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            return button;
        }

        private void SelectTab(int index)
        {
            if (index < 0 || index > 3) return;
            if (_activeTab == 1 && index != 1) CommitUdpFields();
            _activeTab = index;

            if (_tabStartPanel == null) return;
            Panel[] panels = new Panel[] { _tabStartPanel, _tabUdpPanel, _tabMonitorPanel, _tabTimePanel };
            Button[] buttons = new Button[] { _tabStartButton, _tabUdpButton, _tabMonitorButton, _tabTimeButton };
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                panels[i].Visible = i == index;
                if (i == index) panels[i].BringToFront();
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                if (i == index)
                {
                    buttons[i].ForeColor = UiTheme.Accent;
                    buttons[i].Font = UiTheme.Font(10f, FontStyle.Bold);
                }
                else
                {
                    buttons[i].ForeColor = UiTheme.TextMuted;
                    buttons[i].Font = UiTheme.Font(10f);
                }
            }

            Button active = buttons[index];
            if (active == null || _tabIndicator == null) return;
            _tabIndicator.Width = active.Width - 44;
            _tabIndicator.Left = active.Left + 22;
            _tabIndicator.BringToFront();
            if (index == 0 || index == 2) InitializeSplitLayout();
        }

        private void BuildShell()
        {
            _header = new Panel();
            _header.Dock = DockStyle.Top;
            _header.Height = 80;
            _header.BackColor = UiTheme.Navy;

            PictureBox logo = new PictureBox();
            logo.Size = new Size(44, 44);
            logo.Location = new Point(26, 16);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            logo.Image = LoadLogo();
            _header.Controls.Add(logo);

            Label title = UiTheme.MakeBoldLabel("HAM 工作台", 18f, Color.White);
            title.Location = new Point(88, 5);
            _header.Controls.Add(title);

            Label subtitle = UiTheme.MakeLabel(
                "BktTimeSync  →  JTDX  →  GridTracker2  →  QLog      方案启动 · 服务检测 · 运行监测",
                8.6f, Color.FromArgb(159, 179, 200));
            subtitle.Location = new Point(90, 41);
            _header.Controls.Add(subtitle);

            _pill = new StatusPill();
            _pill.SetState("就绪", UiTheme.Accent, Color.White);
            _pill.Location = new Point(1000, 15);
            _header.Controls.Add(_pill);

            _headerSaveHint = UiTheme.MakeLabel("v" + Program.ProductVersion + "  ·  配置自动保存", 8.5f, Color.FromArgb(110, 134, 163));
            _headerSaveHint.Location = new Point(1000, 48);
            _header.Controls.Add(_headerSaveHint);

            _header.Resize += delegate
            {
                _pill.Location = new Point(_header.Width - _pill.Width - 30, 15);
                _headerSaveHint.Location = new Point(_header.Width - _headerSaveHint.Width - 32, 50);
            };

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = UiTheme.Bg;
            body.Padding = new Padding(16, 10, 16, 8);

            Panel tabHost = new Panel();
            tabHost.Dock = DockStyle.Fill;
            tabHost.BackColor = UiTheme.Bg;

            _tabBar = new Panel();
            _tabBar.Dock = DockStyle.Top;
            _tabBar.Height = 42;
            _tabBar.BackColor = Color.White;

            Panel tabLine = new Panel();
            tabLine.Dock = DockStyle.Bottom;
            tabLine.Height = 1;
            tabLine.BackColor = UiTheme.Border;
            _tabBar.Controls.Add(tabLine);

            _tabStartButton = MakeTabButton("启动顺序", 14);
            _tabStartButton.Click += delegate { SelectTab(0); };
            _tabBar.Controls.Add(_tabStartButton);

            _tabUdpButton = MakeTabButton("连接联动", 142);
            _tabUdpButton.Click += delegate { SelectTab(1); };
            _tabBar.Controls.Add(_tabUdpButton);

            _tabMonitorButton = MakeTabButton("运行监测", 270);
            _tabMonitorButton.Click += delegate { SelectTab(2); };
            _tabBar.Controls.Add(_tabMonitorButton);

            _tabTimeButton = MakeTabButton("授时", 398);
            _tabTimeButton.Click += delegate { SelectTab(3); };
            _tabBar.Controls.Add(_tabTimeButton);

            _tabIndicator = new Panel();
            _tabIndicator.Height = 3;
            _tabIndicator.Width = 82;
            _tabIndicator.Left = 36;
            _tabIndicator.Top = 39;
            _tabIndicator.BackColor = UiTheme.Accent;
            _tabBar.Controls.Add(_tabIndicator);

            Label tabHint = UiTheme.MakeLabel("所有修改即时保存", 8.3f, UiTheme.TextFaint);
            tabHint.Location = new Point(500, 14);
            _tabBar.Controls.Add(tabHint);

            _tabContent = new Panel();
            _tabContent.Dock = DockStyle.Fill;
            _tabContent.BackColor = UiTheme.Bg;
            _tabContent.Padding = new Padding(10, 8, 10, 4);

            _tabStartPanel = new Panel();
            _tabStartPanel.Dock = DockStyle.Fill;
            _tabStartPanel.BackColor = UiTheme.Bg;

            _tabUdpPanel = new Panel();
            _tabUdpPanel.Dock = DockStyle.Fill;
            _tabUdpPanel.BackColor = UiTheme.Bg;
            _tabUdpPanel.Visible = false;

            _tabMonitorPanel = new Panel();
            _tabMonitorPanel.Dock = DockStyle.Fill;
            _tabMonitorPanel.BackColor = UiTheme.Bg;
            _tabMonitorPanel.Visible = false;

            _tabTimePanel = new Panel();
            _tabTimePanel.Dock = DockStyle.Fill;
            _tabTimePanel.BackColor = UiTheme.Bg;
            _tabTimePanel.Visible = false;

            BuildStartupTab(_tabStartPanel);
            BuildUdpTab(_tabUdpPanel);
            BuildMonitorTab(_tabMonitorPanel);
            BuildTimeSyncTab(_tabTimePanel);
            BuildUiModeSelector(_tabBar);

            _tabContent.Controls.Add(_tabTimePanel);
            _tabContent.Controls.Add(_tabMonitorPanel);
            _tabContent.Controls.Add(_tabUdpPanel);
            _tabContent.Controls.Add(_tabStartPanel);

            tabHost.Controls.Add(_tabContent);
            tabHost.Controls.Add(_tabBar);
            body.Controls.Add(tabHost);

            Panel logPanel = BuildLogPanel();
            logPanel.Dock = DockStyle.Fill;

            _mainSplit = new SplitContainer();
            _mainSplit.Dock = DockStyle.Fill;
            _mainSplit.Orientation = Orientation.Horizontal;
            _mainSplit.SplitterWidth = 7;
            _mainSplit.Panel1MinSize = 380;
            _mainSplit.Panel2MinSize = 70;
            _mainSplit.BackColor = Color.FromArgb(190, 203, 220);
            _mainSplit.Panel1.Controls.Add(body);
            _mainSplit.Panel2.Controls.Add(logPanel);
            _resizeTip.SetToolTip(_mainSplit, "拖动分隔条调整上方内容与运行日志的高度");

            Controls.Add(_mainSplit);
            Controls.Add(_header);

            Shown += delegate
            {
                try
                {
                    int distance = _mainSplit.Height - 118;
                    _mainSplit.SplitterDistance = Math.Max(_mainSplit.Panel1MinSize,
                        Math.Min(distance, _mainSplit.Height - _mainSplit.Panel2MinSize - _mainSplit.SplitterWidth));
                }
                catch { }
            };

            SelectTab(0);
        }

        private void InitializeSplitLayout()
        {
            try
            {
                if (_mainSplit != null && _mainSplit.IsHandleCreated && !_mainSplitInitialized && _mainSplit.Height > 300)
                {
                    int distance = _mainSplit.Height - 118;
                    _mainSplit.SplitterDistance = Math.Max(_mainSplit.Panel1MinSize,
                        Math.Min(distance, _mainSplit.Height - _mainSplit.Panel2MinSize - _mainSplit.SplitterWidth));
                    _mainSplitInitialized = true;
                }
            }
            catch { }

            try
            {
                if (_startupSplit != null && _startupSplit.IsHandleCreated && !_startupSplitInitialized && _startupSplit.Width > 1000)
                {
                    _startupSplit.SplitterDistance = Math.Max(320, _startupSplit.Width - 352);
                    _startupSplitInitialized = true;
                }
            }
            catch { }

            try
            {
                if (_monitorSplit != null && _monitorSplit.IsHandleCreated && !_monitorSplitInitialized && _monitorSplit.Width > 1000)
                {
                    _monitorSplit.SplitterDistance = Math.Max(360, _monitorSplit.Width - 390);
                    _monitorSplitInitialized = true;
                }
            }
            catch { }
        }

        private Image LoadLogo()
        {
            try
            {
                using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("HamStartup.app.png"))
                {
                    if (stream != null)
                    {
                        using (Image image = Image.FromStream(stream))
                        {
                            return new Bitmap(image);
                        }
                    }
                }
            }
            catch { }
            try { return Icon.ToBitmap(); }
            catch { return null; }
        }

        private Panel BuildLogPanel()
        {
            Panel outer = new Panel();
            outer.Dock = DockStyle.Bottom;
            outer.Height = 110;
            outer.BackColor = Color.FromArgb(11, 18, 32);

            Panel accent = new Panel();
            accent.Dock = DockStyle.Top;
            accent.Height = 2;
            accent.BackColor = Color.FromArgb(30, 58, 95);
            outer.Controls.Add(accent);

            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 30;
            bar.BackColor = Color.FromArgb(11, 18, 32);

            Label caption = UiTheme.MakeBoldLabel("运行日志", 9f, Color.FromArgb(147, 197, 253));
            caption.Location = new Point(20, 6);
            bar.Controls.Add(caption);

            LinkLabel clear = new LinkLabel();
            clear.Text = "清空";
            clear.AutoSize = true;
            clear.Font = UiTheme.Font(8.5f);
            clear.LinkColor = Color.FromArgb(125, 211, 252);
            clear.ActiveLinkColor = Color.White;
            clear.LinkBehavior = LinkBehavior.HoverUnderline;
            clear.BackColor = Color.Transparent;
            clear.Location = new Point(1010, 8);
            clear.LinkClicked += delegate { _log.Clear(); };
            bar.Controls.Add(clear);

            LinkLabel openConfig = new LinkLabel();
            openConfig.Text = "打开配置目录";
            openConfig.AutoSize = true;
            openConfig.Font = UiTheme.Font(8.5f);
            openConfig.LinkColor = Color.FromArgb(125, 211, 252);
            openConfig.ActiveLinkColor = Color.White;
            openConfig.LinkBehavior = LinkBehavior.HoverUnderline;
            openConfig.BackColor = Color.Transparent;
            openConfig.Location = new Point(900, 8);
            openConfig.LinkClicked += delegate
            {
                try
                {
                    string dir = Path.GetDirectoryName(_configPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    ProcessUtil.OpenFolder(dir);
                }
                catch { }
            };
            bar.Controls.Add(openConfig);

            bar.Resize += delegate
            {
                openConfig.Location = new Point(bar.Width - 150, 8);
                clear.Location = new Point(bar.Width - 52, 8);
            };

            _log = new RichTextBox();
            _log.Dock = DockStyle.Fill;
            _log.BackColor = Color.FromArgb(11, 18, 32);
            _log.ForeColor = Color.FromArgb(168, 183, 204);
            _log.BorderStyle = BorderStyle.None;
            _log.Font = new Font("Consolas", 9.5f);
            _log.ReadOnly = true;
            _log.WordWrap = false;
            _log.DetectUrls = false;
            _log.ScrollBars = RichTextBoxScrollBars.Vertical;
            _log.Margin = new Padding(0);
            _log.Location = new Point(20, 34);
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            Panel logHost = new Panel();
            logHost.Dock = DockStyle.Fill;
            logHost.BackColor = Color.FromArgb(11, 18, 32);
            logHost.Padding = new Padding(20, 2, 20, 10);
            logHost.Controls.Add(_log);
            _log.Dock = DockStyle.Fill;

            outer.Controls.Add(logHost);
            outer.Controls.Add(bar);
            return outer;
        }

        private void LoadSettings()
        {
            _settings = AppSettings.Load(_configPath);
            bool changed = false;
            foreach (AppEntry app in _settings.Apps)
            {
                if (!string.IsNullOrEmpty(app.ExePath) && File.Exists(app.ExePath)) continue;
                string found = AppDetector.Find(app, false);
                if (found != null)
                {
                    app.ExePath = found;
                    changed = true;
                    AppendLog("检测到 " + app.Name + "：" + found, LogKind.Info);
                }
                else
                {
                    AppendLog("未检测到 " + app.Name + "，请在“启动顺序”页手动指定程序路径。", LogKind.Warn);
                }
            }
            if (changed) TrySave();
        }

        private bool TrySave()
        {
            try
            {
                _settings.Save();
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("保存设置失败：" + ex.Message, LogKind.Error);
                return false;
            }
        }

        public void AppendLog(string message, LogKind kind)
        {
            if (_log == null || _log.IsDisposed) return;

            string stamp = DateTime.Now.ToString("HH:mm:ss");
            string line = stamp + "  " + message;

            Color color;
            switch (kind)
            {
                case LogKind.Success: color = Color.FromArgb(110, 231, 168); break;
                case LogKind.Warn: color = Color.FromArgb(251, 191, 36); break;
                case LogKind.Error: color = Color.FromArgb(248, 113, 113); break;
                case LogKind.Step: color = Color.FromArgb(147, 197, 253); break;
                default: color = Color.FromArgb(168, 183, 204); break;
            }

            try
            {
                _log.SelectionStart = _log.TextLength;
                _log.SelectionLength = 0;
                _log.SelectionColor = color;
                _log.AppendText(line + Environment.NewLine);
                _log.SelectionColor = _log.ForeColor;
                _log.ScrollToCaret();
            }
            catch { }

            WriteLogFile(line, kind);
        }

        private void WriteLogFile(string line, LogKind kind)
        {
            try
            {
                string level;
                switch (kind)
                {
                    case LogKind.Success: level = "[OK]"; break;
                    case LogKind.Warn: level = "[WARN]"; break;
                    case LogKind.Error: level = "[ERR]"; break;
                    case LogKind.Step: level = "[STEP]"; break;
                    default: level = "[INFO]"; break;
                }

                string dir = Path.Combine(Path.GetDirectoryName(_configPath), "logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "launcher-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                lock (LogFileLock)
                {
                    File.AppendAllText(file, level + " " + line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { }
        }

        private void OnLauncherLog(string message, LogKind kind)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string, LogKind>(OnLauncherLog), message, kind);
                }
                else
                {
                    AppendLog(message, kind);
                }
            }
            catch { }
        }

        private void RefreshStatus()
        {
            if (_running || _list == null) return;

            int running = 0;
            for (int i = 0; i < _settings.Apps.Count && i < _list.Items.Count; i++)
            {
                AppEntry app = _settings.Apps[i];
                ListViewItem item = _list.Items[i];
                bool isRunning = ProcessUtil.IsRunning(app);
                if (isRunning) running++;

                if (item.SubItems.Count > 3)
                {
                    item.SubItems[3].Text = isRunning ? "运行中" : "未运行";
                    item.SubItems[3].ForeColor = isRunning ? UiTheme.Green : UiTheme.TextFaint;
                }
            }

            UpdateHeaderPill(running);
            UpdateRunHint();

            AppEntry selected = SelectedApp();
            if (selected != null)
            {
                bool isRunning = ProcessUtil.IsRunning(selected);
                _detailStatus.Text = isRunning ? "● 正在运行" : "○ 未运行";
                _detailStatus.ForeColor = isRunning ? UiTheme.Green : UiTheme.TextFaint;
            }
            if (_list != null) _list.Refresh();
            UpdateStopButton();
        }

        private void UpdateHeaderPill(int running)
        {
            if (_running)
            {
                _pill.SetState("启动中…", UiTheme.Amber, Color.White);
                return;
            }
            if (running > 0)
                _pill.SetState(string.Format("{0} 个运行中", running), UiTheme.Green, Color.White);
            else
                _pill.SetState("就绪", UiTheme.Accent, Color.White);
        }

        private async void StartSequence()
        {
            if (_running) return;
            CommitUdpFields();

            List<AppEntry> enabled = new List<AppEntry>();
            foreach (AppEntry a in _settings.Apps)
            {
                if (a.Enabled) enabled.Add(a);
            }

            if (enabled.Count == 0)
            {
                MessageBox.Show(this, "请先在列表中勾选要启动的软件。", "没有可启动的软件",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (AppEntry a in enabled)
            {
                if (a.PathExists) continue;
                MessageBox.Show(this,
                    string.Format("“{0}”的程序路径无效：\n{1}\n\n请在该项右侧设置路径，或点击“自动检测”。",
                        a.Name, string.IsNullOrEmpty(a.ExePath) ? "(空)" : a.ExePath),
                    "程序路径缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_settings.Udp.Enabled)
            {
                UdpSnapshot snapshot = UdpConfig.ReadAll();
                List<string> issues = snapshot.Mismatches(_settings.Udp);
                if (issues.Count > 0)
                {
                    AppendLog("UDP 配置检查：发现 " + issues.Count + " 处与推荐值不一致。", LogKind.Warn);
                    foreach (string issue in issues) AppendLog("  · " + issue, LogKind.Warn);
                    if (!_autoStart)
                    {
                        string text = "检测到 UDP 配置与推荐值不一致，联动可能无法正常工作：\n\n" +
                                      string.Join("\n", issues.ToArray()) +
                                      "\n\n可以先到“连接联动”页一键应用，或仍然继续启动。\n是否继续启动？";
                        DialogResult result = MessageBox.Show(this, text, "连接联动提示",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (result != DialogResult.Yes) return;
                    }
                }
                else
                {
                    AppendLog("UDP 配置检查通过，三套软件端口一致。", LogKind.Success);
                }
            }

            TrySave();
            _running = true;
            SetRunningUi(true);
            _cts = new CancellationTokenSource();
            _launcher = new Launcher(_settings, OnLauncherLog);

            AppendLog("════════ 开始启动 ════════", LogKind.Step);
            try
            {
                if (_settings.LaunchAllAtOnce)
                    await _launcher.RunAllAtOnceAsync(_cts.Token);
                else
                    await _launcher.RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                AppendLog("启动流程已取消。", LogKind.Warn);
            }
            catch (Exception ex)
            {
                AppendLog("启动流程异常：" + ex.Message, LogKind.Error);
            }
            finally
            {
                _running = false;
                SetRunningUi(false);
                RefreshStatus();
                if (_startupCardView != null && _startupCardView.Visible) RefreshStartupCards();
            }

            try { StartupCompleted(); }
            catch (Exception ex) { AppendLog("服务检测异常：" + ex.Message, LogKind.Warn); }

            if (_quitAfterStart)
            {
                await System.Threading.Tasks.Task.Delay(900);
                Close();
            }
        }

        private void SetRunningUi(bool running)
        {
            _btnStart.Enabled = !running;
            _list.Enabled = !running;
            _chkOptionsWait.Enabled = !running;
            _chkOptionsSkip.Enabled = !running;
            _chkOptionsFocus.Enabled = !running;
            _chkOptionsClose.Enabled = !running;
            if (_chkOptionsAllAtOnce != null) _chkOptionsAllAtOnce.Enabled = !running;
            _runHint.Text = running
                ? (_settings.LaunchAllAtOnce ? "正在同时启动全部软件，请稍候…" : "正在按顺序启动，请稍候…")
                : BuildRunHintText();
            _runHint.ForeColor = running ? UiTheme.Accent : UiTheme.TextMuted;
            UpdateStopButton();
            UpdateHeaderPill(0);
        }

        private void UpdateStopButton()
        {
            if (_btnStop == null) return;
            bool enabled = _running || (_launcher != null && _launcher.HasStoppableTargets());
            _btnStop.Enabled = enabled;
            _btnStop.ForeColor = enabled ? UiTheme.Text : UiTheme.TextFaint;
            _btnStop.BackColor = enabled ? Color.White : Color.FromArgb(246, 248, 251);
            _btnStop.Cursor = enabled ? Cursors.Hand : Cursors.Default;
            if (_resizeTip != null)
                _resizeTip.SetToolTip(_btnStop, enabled ? "关闭当前方案中正在运行的软件" : "当前没有正在运行的软件");
        }

        private async void StopSequence()
        {
            if (_launcher == null) _launcher = new Launcher(_settings, OnLauncherLog);
            if (!_running && !_launcher.HasStoppableTargets())
            {
                MessageBox.Show(this, "当前没有正在运行的目标软件。", "停止全部",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(this,
                "将关闭当前方案中正在运行的软件，包括启动前已运行的实例和本次由启动器打开的软件。\n是否继续？",
                "停止全部", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            if (_cts != null) _cts.Cancel();
            if (_inspector != null) _inspector.MuteAlertsUntil = DateTime.Now.AddSeconds(20);
            _btnStop.Enabled = false;
            _runHint.Text = "正在关闭全部软件…";
            await _launcher.StopAllAsync();
            UpdateStopButton();
            RefreshStatus();
            if (_startupCardView != null && _startupCardView.Visible) RefreshStartupCards();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_running)
            {
                DialogResult result = MessageBox.Show(this, "启动流程正在进行，确定要退出吗？",
                    "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                if (_cts != null) _cts.Cancel();
            }

            CommitUdpFields();
            TrySave();
            if (_statusTimer != null) _statusTimer.Stop();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }

            if (_settings.CloseAppsOnExit && _launcher != null)
            {
                bool any = false;
                foreach (Process p in _launcher.StartedProcesses)
                {
                    try { if (!p.HasExited) { any = true; break; } }
                    catch { }
                }
                if (any) _launcher.StopAllSync();
            }
        }

        private void UpdateRunHint()
        {
            if (_runHint == null) return;
            if (!_running) _runHint.Text = BuildRunHintText();
        }

        private string BuildRunHintText()
        {
            int count = 0;
            foreach (AppEntry a in _settings.Apps)
            {
                if (a.Enabled) count++;
            }
            if (count == 0) return "尚未勾选软件";
            return string.Format("已勾选 {0} 个软件 · 通常 30–60 秒完成", count);
        }
    }
}




