using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HamStartup
{
    public partial class MainForm
    {
        private ComboBox _uiModeCombo;
        private Panel _startupContentHost;
        private Control _startupClassicContent;
        private Panel _startupCardView;
        private TableLayoutPanel _startupCardTable;
        private TopologyView _startupTopology;
        private FlowLayoutPanel _startupCards;
        private bool _loadingStartupCards;
        private AppEntry _dragCardApp;

        private Panel _udpContentHost;
        private Panel _udpCardView;
        private TableLayoutPanel _connectionCardTable;
        private TopologyView _connectionTopology;
        private FlowLayoutPanel _connectionCards;

        private Panel _monitorContentHost;
        private Control _monitorClassicContent;
        private Panel _monitorCardView;
        private TableLayoutPanel _monitorCardTable;
        private TopologyView _monitorTopology;
        private FlowLayoutPanel _monitorCards;

        private Panel BuildStartupCardView()
        {
            Panel view = new Panel();
            view.Dock = DockStyle.Fill;
            view.BackColor = UiTheme.Bg;

            _startupTopology = new TopologyView();
            _startupTopology.Dock = DockStyle.Fill;

            _startupCards = new FlowLayoutPanel();
            _startupCards.Dock = DockStyle.Fill;
            _startupCards.FlowDirection = FlowDirection.TopDown;
            _startupCards.WrapContents = false;
            _startupCards.AutoScroll = true;
            _startupCards.Padding = new Padding(2, 2, 2, 8);
            _startupCards.BackColor = UiTheme.Bg;
            _startupCards.Resize += delegate { ResizeStartupCards(); };
            _startupCards.AllowDrop = true;
            _startupCards.DragEnter += delegate(object sender, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(typeof(AppEntry)) ? DragDropEffects.Move : DragDropEffects.None;
            };
            _startupCards.DragDrop += delegate(object sender, DragEventArgs e)
            {
                AppEntry source = e.Data.GetData(typeof(AppEntry)) as AppEntry;
                if (source == null) return;
                Point point = _startupCards.PointToClient(new Point(e.X, e.Y));
                Control child = _startupCards.GetChildAtPoint(point);
                AppEntry target = child == null ? null : child.Tag as AppEntry;
                int sourceIndex = _settings.Apps.IndexOf(source);
                int targetIndex = target == null ? _settings.Apps.Count - 1 : _settings.Apps.IndexOf(target);
                MoveAppTo(sourceIndex, targetIndex);
                _dragCardApp = null;
            };

            _startupCardTable = new TableLayoutPanel();
            _startupCardTable.Dock = DockStyle.Fill;
            _startupCardTable.ColumnCount = 1;
            _startupCardTable.RowCount = 2;
            _startupCardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));
            _startupCardTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _startupCardTable.Controls.Add(_startupTopology, 0, 0);
            _startupCardTable.Controls.Add(_startupCards, 0, 1);
            view.Controls.Add(_startupCardTable);
            return view;
        }

        private void RefreshStartupCards()
        {
            if (_startupCards == null) return;
            _loadingStartupCards = true;
            _startupCards.SuspendLayout();
            while (_startupCards.Controls.Count > 0) _startupCards.Controls[0].Dispose();
            _startupCards.Controls.Clear();
            int width = Math.Max(500, _startupCards.ClientSize.Width - 16);
            foreach (AppEntry app in _settings.Apps)
            {
                CardPanel card = new CardPanel();
                card.Width = width;
                card.Height = 88;
                card.Margin = new Padding(4, 2, 4, 8);
                card.Tag = app;
                card.MouseDown += delegate(object sender, MouseEventArgs e)
                {
                    if (e.Button != MouseButtons.Left) return;
                    _dragCardApp = app;
                    card.DoDragDrop(app, DragDropEffects.Move);
                };

                CheckBox enabled = new CheckBox();
                enabled.Text = app.Name;
                enabled.Font = UiTheme.Font(10f, FontStyle.Bold);
                enabled.ForeColor = UiTheme.Text;
                enabled.BackColor = Color.White;
                enabled.AutoSize = true;
                enabled.Checked = app.Enabled;
                enabled.Location = new Point(14, 10);
                enabled.CheckedChanged += delegate
                {
                    if (_loadingStartupCards) return;
                    app.Enabled = enabled.Checked;
                    TrySave();
                    UpdateRunHint();
                };
                card.Controls.Add(enabled);

                Label path = new Label();
                path.AutoSize = false;
                path.AutoEllipsis = true;
                path.Text = string.IsNullOrEmpty(app.ExePath) ? "未设置路径" : app.ExePath;
                path.Font = UiTheme.Font(8.2f);
                path.ForeColor = app.PathExists ? UiTheme.TextMuted : UiTheme.Red;
                path.BackColor = Color.White;
                path.Location = new Point(14, 36);
                path.Width = Math.Max(200, width - 330);
                path.Height = 18;
                card.Controls.Add(path);

                Label meta = new Label();
                meta.AutoSize = true;
                meta.Text = string.Format("{0}   ·   窗口 {1}s + 延时 {2}s",
                    ProcessUtil.IsRunning(app) ? "运行中" : "未运行",
                    app.WaitForWindow ? app.WaitSeconds : 0, app.DelaySeconds);
                meta.Font = UiTheme.Font(8.3f);
                meta.ForeColor = ProcessUtil.IsRunning(app) ? UiTheme.Green : UiTheme.TextFaint;
                meta.BackColor = Color.White;
                meta.Location = new Point(14, 60);
                card.Controls.Add(meta);

                Button up = UiTheme.SmallButton("↑", 34, 28);
                up.Location = new Point(width - 244, 14);
                up.Click += delegate { MoveCardApp(app, -1); };
                card.Controls.Add(up);

                Button down = UiTheme.SmallButton("↓", 34, 28);
                down.Location = new Point(width - 206, 14);
                down.Click += delegate { MoveCardApp(app, 1); };
                card.Controls.Add(down);

                Button edit = UiTheme.SmallButton("编辑", 56, 28);
                edit.Location = new Point(width - 166, 14);
                edit.Click += delegate { EditAppFromCard(app); };
                card.Controls.Add(edit);

                Button delete = UiTheme.SmallButton("删除", 56, 28);
                delete.Location = new Point(width - 104, 14);
                delete.Click += delegate { DeleteAppFromCard(app); };
                card.Controls.Add(delete);

                Label grip = new Label();
                grip.Text = "⋮⋮";
                grip.AutoSize = false;
                grip.Width = 26;
                grip.Height = 28;
                grip.TextAlign = ContentAlignment.MiddleCenter;
                grip.Font = UiTheme.Font(10f, FontStyle.Bold);
                grip.ForeColor = UiTheme.TextFaint;
                grip.BackColor = Color.White;
                grip.Cursor = Cursors.SizeAll;
                grip.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                grip.Location = new Point(width - 278, 14);
                grip.MouseDown += delegate(object sender, MouseEventArgs e)
                {
                    if (e.Button != MouseButtons.Left) return;
                    _dragCardApp = app;
                    card.DoDragDrop(app, DragDropEffects.Move);
                };
                card.Controls.Add(grip);
                if (_resizeTip != null) _resizeTip.SetToolTip(grip, "按住拖动调整启动顺序");

                _startupCards.Controls.Add(card);
            }
            _startupCards.ResumeLayout();
            _loadingStartupCards = false;

            if (_startupTopology != null)
            {
                List<string> apps = new List<string>();
                foreach (AppEntry app in _settings.Apps)
                    apps.Add((app.Enabled ? "✓ " : "○ ") + app.Name);
                _startupTopology.SetData("启动方案 · " + _settings.ActiveScheme.Name, apps, new List<string>());
            }
        }

        private void ResizeStartupCards()
        {
            if (_startupCards == null) return;
            int width = Math.Max(500, _startupCards.ClientSize.Width - 16);
            foreach (Control control in _startupCards.Controls)
                control.Width = width;
        }

        private void MoveCardApp(AppEntry app, int direction)
        {
            int index = _settings.Apps.IndexOf(app);
            int target = index + direction;
            if (index < 0 || target < 0 || target >= _settings.Apps.Count) return;
            AppEntry temp = _settings.Apps[index];
            _settings.Apps[index] = _settings.Apps[target];
            _settings.Apps[target] = temp;
            TrySave();
            BuildAppList();
        }

        private void EditAppFromCard(AppEntry app)
        {
            if (_uiModeCombo != null) _uiModeCombo.SelectedIndex = 0;
            foreach (ListViewItem item in _list.Items)
            {
                if (object.ReferenceEquals(item.Tag, app))
                {
                    item.Selected = true;
                    item.Focused = true;
                    break;
                }
            }
            UpdateDetail();
        }

        private void DeleteAppFromCard(AppEntry app)
        {
            if (MessageBox.Show(this, "从当前方案中删除“" + app.Name + "”？", "删除软件",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _settings.Apps.Remove(app);
            TrySave();
            BuildAppList();
        }

        private Panel BuildConnectionCardView()
        {
            Panel view = new Panel();
            view.Dock = DockStyle.Fill;
            view.BackColor = UiTheme.Bg;

            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.Height = 38;
            bar.BackColor = Color.White;

            Label title = UiTheme.MakeBoldLabel("连接方案卡片", 9.5f, UiTheme.Text);
            title.Location = new Point(12, 10);
            bar.Controls.Add(title);

            Button add = UiTheme.SmallButton("添加方案", 84, 28);
            add.Location = new Point(150, 5);
            add.Click += delegate { AddLinkProfile(); };
            bar.Controls.Add(add);

            Button download = UiTheme.SmallButton("参数下载", 84, 28);
            download.Location = new Point(240, 5);
            download.Click += delegate { DownloadLinkParameters(); };
            bar.Controls.Add(download);

            Button classic = UiTheme.SmallButton("经典设置", 84, 28);
            classic.Location = new Point(330, 5);
            classic.Click += delegate { if (_uiModeCombo != null) _uiModeCombo.SelectedIndex = 0; };
            bar.Controls.Add(classic);

            _connectionTopology = new TopologyView();
            _connectionTopology.Dock = DockStyle.Fill;

            _connectionCards = new FlowLayoutPanel();
            _connectionCards.Dock = DockStyle.Fill;
            _connectionCards.FlowDirection = FlowDirection.TopDown;
            _connectionCards.WrapContents = false;
            _connectionCards.AutoScroll = true;
            _connectionCards.Padding = new Padding(2, 2, 2, 8);
            _connectionCards.BackColor = UiTheme.Bg;
            _connectionCards.Resize += delegate { ResizeConnectionCards(); };

            _connectionCardTable = new TableLayoutPanel();
            _connectionCardTable.Dock = DockStyle.Fill;
            _connectionCardTable.ColumnCount = 1;
            _connectionCardTable.RowCount = 3;
            _connectionCardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            _connectionCardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));
            _connectionCardTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _connectionCardTable.Controls.Add(bar, 0, 0);
            _connectionCardTable.Controls.Add(_connectionTopology, 0, 1);
            _connectionCardTable.Controls.Add(_connectionCards, 0, 2);
            view.Controls.Add(_connectionCardTable);
            return view;
        }

        private void RefreshConnectionCards()
        {
            if (_connectionCards == null) return;
            if (_udpCardView != null && !_udpCardView.Visible) return;
            _connectionCards.SuspendLayout();
            while (_connectionCards.Controls.Count > 0) _connectionCards.Controls[0].Dispose();
            _connectionCards.Controls.Clear();
            int width = Math.Max(500, _connectionCards.ClientSize.Width - 16);

            foreach (LinkProfile profile in _settings.Links)
            {
                LinkMatchResult match = null;
                foreach (LinkMatchResult m in _linkMatches)
                {
                    if (m.Profile.Id == profile.Id) { match = m; break; }
                }
                CardPanel card = new CardPanel();
                card.Width = width;
                card.Height = 92;
                card.Margin = new Padding(4, 2, 4, 8);

                Label name = UiTheme.MakeBoldLabel(profile.Name, 10f, UiTheme.Text);
                name.Location = new Point(14, 10);
                name.AutoSize = true;
                card.Controls.Add(name);

                Label proto = UiTheme.MakeLabel(profile.Protocol + "   " + profile.Direction, 8.5f, UiTheme.Accent);
                proto.Location = new Point(14, 38);
                card.Controls.Add(proto);

                Label endpoint = new Label();
                endpoint.AutoSize = false;
                endpoint.AutoEllipsis = true;
                endpoint.Text = profile.AppMatch + "   " + profile.EndpointText;
                endpoint.Font = UiTheme.Font(8.4f);
                endpoint.ForeColor = UiTheme.TextMuted;
                endpoint.BackColor = Color.White;
                endpoint.Location = new Point(14, 58);
                endpoint.Width = width - 300;
                endpoint.Height = 18;
                card.Controls.Add(endpoint);

                Label status = UiTheme.MakeLabel(
                    match == null ? "等待检测" : (match.Matched ? "已匹配：" + match.Detail : match.Detail),
                    8.4f, match != null && match.Matched ? UiTheme.Green : UiTheme.TextFaint);
                status.Location = new Point(width - 230, 58);
                card.Controls.Add(status);

                Button edit = UiTheme.SmallButton("编辑", 56, 28);
                edit.Location = new Point(width - 130, 14);
                edit.Click += delegate { EditLinkProfile(profile); };
                card.Controls.Add(edit);

                Button del = UiTheme.SmallButton("删除", 56, 28);
                del.Location = new Point(width - 68, 14);
                del.Click += delegate { DeleteLinkProfile(profile); };
                card.Controls.Add(del);

                _connectionCards.Controls.Add(card);
            }
            _connectionCards.ResumeLayout();

            if (_connectionTopology != null)
            {
                List<string> apps = new List<string>();
                if (_monitorSnapshot != null)
                {
                    foreach (RuntimeAppInfo app in _monitorSnapshot.Apps)
                        if (app.Running) apps.Add(app.Name);
                }
                List<string> links = new List<string>();
                foreach (LinkProfile profile in _settings.Links)
                    if (profile.Enabled) links.Add(profile.Protocol + " " + profile.Name);
                _connectionTopology.SetData("连接联动", apps, links);
            }
        }

        private void ResizeConnectionCards()
        {
            if (_connectionCards == null) return;
            int width = Math.Max(500, _connectionCards.ClientSize.Width - 16);
            foreach (Control control in _connectionCards.Controls)
                control.Width = width;
        }

        private Panel BuildMonitorCardView()
        {
            Panel view = new Panel();
            view.Dock = DockStyle.Fill;
            view.BackColor = UiTheme.Bg;

            _monitorTopology = new TopologyView();
            _monitorTopology.Dock = DockStyle.Fill;

            _monitorCards = new FlowLayoutPanel();
            _monitorCards.Dock = DockStyle.Fill;
            _monitorCards.FlowDirection = FlowDirection.TopDown;
            _monitorCards.WrapContents = false;
            _monitorCards.AutoScroll = true;
            _monitorCards.Padding = new Padding(2, 2, 2, 8);
            _monitorCards.BackColor = UiTheme.Bg;
            _monitorCards.Resize += delegate { ResizeMonitorCards(); };

            _monitorCardTable = new TableLayoutPanel();
            _monitorCardTable.Dock = DockStyle.Fill;
            _monitorCardTable.ColumnCount = 1;
            _monitorCardTable.RowCount = 2;
            _monitorCardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));
            _monitorCardTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _monitorCardTable.Controls.Add(_monitorTopology, 0, 0);
            _monitorCardTable.Controls.Add(_monitorCards, 0, 1);
            view.Controls.Add(_monitorCardTable);
            return view;
        }

        private void RefreshMonitorCards()
        {
            if (_monitorCards == null || _monitorSnapshot == null) return;
            if (_monitorCardView != null && !_monitorCardView.Visible) return;
            _monitorCards.SuspendLayout();
            while (_monitorCards.Controls.Count > 0) _monitorCards.Controls[0].Dispose();
            _monitorCards.Controls.Clear();
            int width = Math.Max(500, _monitorCards.ClientSize.Width - 16);

            foreach (RuntimeAppInfo app in _monitorSnapshot.Apps)
            {
                CardPanel card = new CardPanel();
                card.Width = width;
                card.Height = 96;
                card.Margin = new Padding(4, 2, 4, 8);

                Label name = UiTheme.MakeBoldLabel(app.Name, 10f, UiTheme.Text);
                name.Location = new Point(14, 10);
                name.AutoSize = true;
                card.Controls.Add(name);

                Label status = UiTheme.MakeLabel(
                    (app.Running ? app.Status : "未启动") +
                    (app.Running && !app.Responding ? "  （无响应）" : ""),
                    8.8f, app.Running ? (app.Status.Contains("无响应") ? UiTheme.Red : UiTheme.Green) : UiTheme.TextFaint);
                status.Location = new Point(14, 38);
                card.Controls.Add(status);

                Label meta = UiTheme.MakeLabel(
                    string.Format("{0}   CPU {1:0}%   内存 {2} MB   UDP {3}   DX {4}",
                        app.Running ? "PID " + app.Pid : "—", app.CpuPercent, app.MemoryMb,
                        app.UdpText, app.ClusterText), 8.2f, UiTheme.TextMuted);
                meta.Location = new Point(14, 60);
                card.Controls.Add(meta);

                Label adi = new Label();
                adi.AutoSize = false;
                adi.AutoEllipsis = true;
                adi.Text = "ADI：" + app.AdiText;
                adi.Font = UiTheme.Font(8f);
                adi.ForeColor = UiTheme.TextFaint;
                adi.BackColor = Color.White;
                adi.Location = new Point(width - 400, 38);
                adi.Width = 300;
                adi.Height = 18;
                card.Controls.Add(adi);

                Button detail = UiTheme.SmallButton("经典详情", 80, 28);
                detail.Location = new Point(width - 100, 14);
                detail.Click += delegate { ShowMonitorClassic(app); };
                card.Controls.Add(detail);

                _monitorCards.Controls.Add(card);
            }
            _monitorCards.ResumeLayout();

            if (_monitorTopology != null)
            {
                List<string> apps = new List<string>();
                foreach (RuntimeAppInfo app in _monitorSnapshot.Apps)
                    if (app.Running) apps.Add(app.Status + " " + app.Name);
                List<string> services = new List<string>();
                services.Add("UDP 端点 " + _monitorSnapshot.UdpCount);
                services.Add("DX 集群 " + _monitorSnapshot.ClusterCount);
                services.Add("ADI 文件 " + _monitorSnapshot.AdiFiles.Count);
                _monitorTopology.SetData("运行监测", apps, services);
            }
        }

        private void ResizeMonitorCards()
        {
            if (_monitorCards == null) return;
            int width = Math.Max(500, _monitorCards.ClientSize.Width - 16);
            foreach (Control control in _monitorCards.Controls)
                control.Width = width;
        }

        private void ShowMonitorClassic(RuntimeAppInfo app)
        {
            if (_uiModeCombo != null) _uiModeCombo.SelectedIndex = 0;
            if (_monitorList == null) return;
            foreach (ListViewItem item in _monitorList.Items)
            {
                RuntimeAppInfo info = item.Tag as RuntimeAppInfo;
                if (info != null && info.Pid == app.Pid)
                {
                    item.Selected = true;
                    item.Focused = true;
                    break;
                }
            }
            UpdateMonitorDetail();
        }

        private void ApplyUiMode()
        {
            if (_uiModeCombo == null || _startupClassicContent == null) return;
            int index = _uiModeCombo.SelectedIndex;
            string mode = index == 0 ? "classic" : (index == 1 ? "cards" : "topology");
            _settings.ViewMode = mode;
            bool cards = index != 0;
            bool topology = index == 2;

            _startupClassicContent.Visible = !cards;
            _startupCardView.Visible = cards;
            if (_startupTopology != null) _startupTopology.Visible = topology;
            if (_startupCardTable != null) _startupCardTable.RowStyles[0].Height = topology ? 164 : 0;
            if (cards) RefreshStartupCards();

            _udpScroll.Visible = !cards;
            _udpCardView.Visible = cards;
            if (_connectionTopology != null) _connectionTopology.Visible = topology;
            if (_connectionCardTable != null) _connectionCardTable.RowStyles[1].Height = topology ? 164 : 0;
            if (cards) RefreshConnectionCards();

            _monitorClassicContent.Visible = !cards;
            _monitorCardView.Visible = cards;
            if (_monitorTopology != null) _monitorTopology.Visible = topology;
            if (_monitorCardTable != null) _monitorCardTable.RowStyles[0].Height = topology ? 164 : 0;
            if (cards) RefreshMonitorCards();

            TrySave();
        }

        private void BuildUiModeSelector(Panel tabBar)
        {
            _uiModeCombo = new ComboBox();
            _uiModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _uiModeCombo.Font = UiTheme.Font(8.8f);
            _uiModeCombo.Width = 126;
            _uiModeCombo.Items.AddRange(new object[] { "经典界面", "卡片", "卡片+网络拓扑" });
            _uiModeCombo.SelectedIndexChanged += delegate
            {
                if (_uiModeCombo.SelectedIndex >= 0) ApplyUiMode();
            };
            tabBar.Controls.Add(_uiModeCombo);
            tabBar.Resize += delegate
            {
                _uiModeCombo.Location = new Point(tabBar.Width - _uiModeCombo.Width - 18, 9);
            };
            _uiModeCombo.Location = new Point(tabBar.Width - _uiModeCombo.Width - 18, 9);

            Label label = UiTheme.MakeLabel("界面", 8.3f, UiTheme.TextFaint);
            label.Location = new Point(_uiModeCombo.Left - 38, 14);
            tabBar.Controls.Add(label);
            tabBar.Resize += delegate { label.Location = new Point(_uiModeCombo.Left - 38, 14); };

            int initial = 0;
            if (_settings.ViewMode == "cards") initial = 1;
            else if (_settings.ViewMode == "topology") initial = 2;
            _uiModeCombo.SelectedIndex = initial;
        }
    }
}
