using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace HamStartup
{
    public partial class MainForm
    {
        private Panel _udpScroll;
        private UdpSnapshot _udpSnapshot;
        private Label _udpOverallTitle;
        private Label _udpOverallDetail;
        private Label _udpListeners;
        private Label _jtdxCurrent;
        private Label _gt2Current;
        private Label _qlogCurrent;
        private Label _jtdxPath;
        private Label _gt2Path;
        private Label _qlogPath;
        private TextBox _jtdxAddr1;
        private TextBox _jtdxAddr2;
        private TextBox _gt2Addr;
        private TextBox _qlogAddr;
        private TextBox _qlogForward;
        private NumericUpDown _jtdxPort1;
        private NumericUpDown _jtdxPort2;
        private NumericUpDown _gt2Port;
        private NumericUpDown _qlogPort;
        private NumericUpDown _qlogTtl;
        private CheckBox _jtdxAccept;
        private CheckBox _gt2Multicast;
        private CheckBox _qlogJoin;
        private CheckBox _chkUdpEnabled;
        private Button _udpApplyAll;
        private Button _udpRefresh;
        private readonly List<Control> _udpCards = new List<Control>();

        private void BuildUdpTab(Panel page)
        {
            _udpScroll = new Panel();
            _udpScroll.Dock = DockStyle.Fill;
            _udpScroll.AutoScroll = true;
            _udpScroll.BackColor = UiTheme.Bg;
            _udpScroll.Padding = new Padding(2, 2, 2, 8);
            page.Controls.Add(_udpScroll);

            _udpCards.Clear();
            _udpScroll.Controls.Add(BuildUdpIntroCard());
            _udpScroll.Controls.Add(BuildUdpStatusCard());
            _udpScroll.Controls.Add(BuildJtdxCard());
            _udpScroll.Controls.Add(BuildGt2Card());
            _udpScroll.Controls.Add(BuildQlogCard());
            _udpScroll.Controls.Add(BuildUdpFooterCard());

            _udpScroll.Resize += delegate { LayoutUdpCards(); };
            LayoutUdpCards();
        }

        private void LayoutUdpCards()
        {
            if (_udpScroll == null) return;
            int width = Math.Max(720, _udpScroll.ClientSize.Width - 30);
            int y = 2;
            foreach (Control card in _udpCards)
            {
                card.Location = new Point(10, y);
                card.Width = width;
                y += card.Height + 12;
            }
            _udpScroll.AutoScrollMinSize = new Size(0, y + 4);
        }

        private CardPanel MakeUdpCard(int height)
        {
            CardPanel card = new CardPanel();
            card.Height = height;
            card.Width = 900;
            _udpCards.Add(card);
            return card;
        }

        private Control BuildUdpIntroCard()
        {
            CardPanel card = MakeUdpCard(256);

            Label title = UiTheme.MakeBoldLabel("UDP 联动是怎么工作的？", 12.5f, UiTheme.Text);
            title.Location = new Point(20, 16);
            card.Controls.Add(title);

            Label desc = new Label();
            desc.AutoSize = false;
            desc.Text = "JTDX 负责解码，并把通联与解码信息通过 UDP 组播发送到 239.255.0.1:2237；\r\nGridTracker2 与 QLog 加入同一个组播组监听 2237 端口，因此三套软件可以同时收到实时数据、互不干扰。";
            desc.Font = UiTheme.Font(9.2f);
            desc.ForeColor = UiTheme.TextMuted;
            desc.BackColor = Color.White;
            desc.Location = new Point(20, 44);
            desc.Height = 48;
            desc.Width = 700;
            card.Controls.Add(desc);

            UdpDiagram diagram = new UdpDiagram();
            diagram.Location = new Point(20, 102);
            diagram.Width = 700;
            diagram.Height = 140;
            card.Controls.Add(diagram);

            card.Resize += delegate
            {
                desc.Width = Math.Max(400, card.ClientSize.Width - 40);
                diagram.Width = Math.Max(500, card.ClientSize.Width - 40);
            };
            return card;
        }

        private Control BuildUdpStatusCard()
        {
            CardPanel card = MakeUdpCard(110);

            _udpOverallTitle = UiTheme.MakeBoldLabel("正在检测 UDP 配置…", 11.5f, UiTheme.Text);
            _udpOverallTitle.Location = new Point(20, 16);
            card.Controls.Add(_udpOverallTitle);

            _udpOverallDetail = new Label();
            _udpOverallDetail.AutoSize = false;
            _udpOverallDetail.AutoEllipsis = true;
            _udpOverallDetail.Font = UiTheme.Font(9f);
            _udpOverallDetail.ForeColor = UiTheme.TextMuted;
            _udpOverallDetail.BackColor = Color.White;
            _udpOverallDetail.Location = new Point(20, 44);
            _udpOverallDetail.Height = 22;
            _udpOverallDetail.Width = 560;
            card.Controls.Add(_udpOverallDetail);

            _udpListeners = UiTheme.MakeLabel("", 8.5f, UiTheme.TextFaint);
            _udpListeners.Location = new Point(20, 74);
            card.Controls.Add(_udpListeners);

            _udpApplyAll = UiTheme.PrimaryButton("一键应用推荐配置", 178, 38);
            _udpApplyAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _udpApplyAll.Click += delegate { ApplyAllUdp(); };
            card.Controls.Add(_udpApplyAll);

            _udpRefresh = UiTheme.SecondaryButton("重新检测", 96, 34);
            _udpRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _udpRefresh.Click += delegate
            {
                RefreshUdpState(false);
                AppendLog("已重新读取三套软件的 UDP 配置。", LogKind.Info);
            };
            card.Controls.Add(_udpRefresh);

            card.Resize += delegate
            {
                _udpApplyAll.Location = new Point(card.ClientSize.Width - 20 - _udpApplyAll.Width, 22);
                _udpRefresh.Location = new Point(_udpApplyAll.Left - 10 - _udpRefresh.Width, 24);
                _udpOverallDetail.Width = Math.Max(260, card.ClientSize.Width - 330);
            };
            return card;
        }

        private Control BuildJtdxCard()
        {
            CardPanel card = MakeUdpCard(224);

            AddCardTitle(card, "JTDX · UDP 发送端",
                "UDP1 把解码信息组播给 GridTracker2 / QLog；UDP2 可同时发送给 N1MM 等额外程序。");

            _jtdxPath = AddPathLabel(card);
            _jtdxCurrent = AddCurrentLabel(card);

            AddFieldLabel(card, "UDP1 地址", 20, 116);
            _jtdxAddr1 = AddTextBox(card, 92, 112, 140);
            AddFieldLabel(card, "端口", 242, 116);
            _jtdxPort1 = AddPortBox(card, 282, 112, 72, 2237);

            AddFieldLabel(card, "UDP2 地址", 366, 116);
            _jtdxAddr2 = AddTextBox(card, 438, 112, 140);
            AddFieldLabel(card, "端口", 588, 116);
            _jtdxPort2 = AddPortBox(card, 628, 112, 72, 2333);

            _jtdxAccept = new CheckBox();
            _jtdxAccept.Text = "接受 UDP 请求";
            _jtdxAccept.AutoSize = true;
            _jtdxAccept.Font = UiTheme.Font(9f);
            _jtdxAccept.BackColor = Color.White;
            _jtdxAccept.Location = new Point(716, 114);
            card.Controls.Add(_jtdxAccept);

            AddHint(card, "推荐：UDP1 = 239.255.0.1:2237（组播），UDP2 = 127.0.0.1:2333。修改前请先关闭 JTDX。", 154);

            Button read = UiTheme.SmallButton("读取当前", 88, 32);
            read.Click += delegate { ReadJtdxToFields(); };
            card.Controls.Add(read);

            Button open = UiTheme.SmallButton("打开目录", 88, 32);
            open.Click += delegate { ProcessUtil.OpenFolder(UdpConfig.JtdxIniPath); };
            card.Controls.Add(open);

            Button apply = UiTheme.SmallButton("应用 JTDX 设置", 132, 32);
            apply.Click += delegate { ApplyJtdx(); };
            card.Controls.Add(apply);

            AnchorButtonsRight(card, 176, apply, open, read);
            return card;
        }

        private Control BuildGt2Card()
        {
            CardPanel card = MakeUdpCard(224);

            AddCardTitle(card, "GridTracker2 · UDP 接收端",
                "加入 239.255.0.1:2237 组播组接收 JTDX 数据；也可把数据转发给 N1MM 等程序。");

            _gt2Path = AddPathLabel(card);
            _gt2Current = AddCurrentLabel(card);

            AddFieldLabel(card, "WSJT-X 地址", 20, 116);
            _gt2Addr = AddTextBox(card, 120, 112, 150);
            AddFieldLabel(card, "端口", 286, 116);
            _gt2Port = AddPortBox(card, 326, 112, 72, 2237);

            _gt2Multicast = new CheckBox();
            _gt2Multicast.Text = "启用组播接收";
            _gt2Multicast.AutoSize = true;
            _gt2Multicast.Font = UiTheme.Font(9f);
            _gt2Multicast.BackColor = Color.White;
            _gt2Multicast.Location = new Point(424, 114);
            card.Controls.Add(_gt2Multicast);

            AddHint(card, "推荐：WSJT-X 地址 239.255.0.1，端口 2237，并启用组播。N1MM 转发端口保持默认即可。", 154);

            Button read = UiTheme.SmallButton("读取当前", 88, 32);
            read.Click += delegate { ReadGt2ToFields(); };
            card.Controls.Add(read);

            Button open = UiTheme.SmallButton("打开目录", 88, 32);
            open.Click += delegate { ProcessUtil.OpenFolder(UdpConfig.Gt2JsonPath); };
            card.Controls.Add(open);

            Button apply = UiTheme.SmallButton("应用 GT2 设置", 132, 32);
            apply.Click += delegate { ApplyGt2(); };
            card.Controls.Add(apply);

            AnchorButtonsRight(card, 176, apply, open, read);
            return card;
        }

        private Control BuildQlogCard()
        {
            CardPanel card = MakeUdpCard(228);

            AddCardTitle(card, "QLog · UDP 接收端",
                "从 2237 组播接收 WSJT-X/JTDX 消息并自动写入日志；转发地址供其他程序使用。");

            _qlogPath = AddPathLabel(card);
            _qlogCurrent = AddCurrentLabel(card);

            AddFieldLabel(card, "监听端口", 20, 116);
            _qlogPort = AddPortBox(card, 96, 112, 76, 2237);

            AddFieldLabel(card, "组播地址", 190, 116);
            _qlogAddr = AddTextBox(card, 262, 112, 145);

            _qlogJoin = new CheckBox();
            _qlogJoin.Text = "加入组播";
            _qlogJoin.AutoSize = true;
            _qlogJoin.Font = UiTheme.Font(9f);
            _qlogJoin.BackColor = Color.White;
            _qlogJoin.Location = new Point(424, 114);
            card.Controls.Add(_qlogJoin);

            AddFieldLabel(card, "TTL", 520, 116);
            _qlogTtl = new NumericUpDown();
            _qlogTtl.Location = new Point(556, 112);
            _qlogTtl.Width = 60;
            _qlogTtl.Minimum = 0;
            _qlogTtl.Maximum = 255;
            _qlogTtl.Font = UiTheme.Font(9f);
            _qlogTtl.BorderStyle = BorderStyle.FixedSingle;
            _qlogTtl.TextAlign = HorizontalAlignment.Right;
            card.Controls.Add(_qlogTtl);

            AddFieldLabel(card, "转发地址", 636, 116);
            _qlogForward = AddTextBox(card, 708, 112, 140);

            AddHint(card, "QLog 的 UDP 设置保存在 qlog.db 中；写入前会先关闭检查，并备份整个数据库（约 58 MB）。", 154);

            Button read = UiTheme.SmallButton("读取当前", 88, 32);
            read.Click += delegate { ReadQlogToFields(); };
            card.Controls.Add(read);

            Button open = UiTheme.SmallButton("打开目录", 88, 32);
            open.Click += delegate { ProcessUtil.OpenFolder(UdpConfig.QlogDbPath); };
            card.Controls.Add(open);

            Button apply = UiTheme.SmallButton("应用 QLog 设置", 132, 32);
            apply.Click += delegate { ApplyQlog(); };
            card.Controls.Add(apply);

            AnchorButtonsRight(card, 180, apply, open, read);
            return card;
        }

        private Control BuildUdpFooterCard()
        {
            CardPanel card = MakeUdpCard(100);
            Label text = new Label();
            text.AutoSize = false;
            text.Text = "推荐流程：关闭三套软件 → 一键应用 → 用启动器顺序启动。所有写入都会先备份到配置目录的 backups\\时间戳 文件夹。";
            text.Font = UiTheme.Font(9f);
            text.ForeColor = UiTheme.TextMuted;
            text.BackColor = Color.White;
            text.Location = new Point(20, 14);
            text.Width = 700;
            text.Height = 22;
            card.Controls.Add(text);

            _chkUdpEnabled = new CheckBox();
            _chkUdpEnabled.Text = "启动前检查 UDP 配置";
            _chkUdpEnabled.AutoSize = true;
            _chkUdpEnabled.Font = UiTheme.Font(9f);
            _chkUdpEnabled.ForeColor = UiTheme.Text;
            _chkUdpEnabled.BackColor = Color.White;
            _chkUdpEnabled.Location = new Point(20, 44);
            _chkUdpEnabled.Checked = _settings.Udp.Enabled;
            _chkUdpEnabled.CheckedChanged += delegate
            {
                _settings.Udp.Enabled = _chkUdpEnabled.Checked;
                TrySave();
                RefreshUdpState(false);
            };
            card.Controls.Add(_chkUdpEnabled);

            Button backup = UiTheme.SmallButton("打开备份目录", 124, 32);
            backup.Location = new Point(240, 40);
            backup.Click += delegate
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_configPath), "backups");
                ProcessUtil.OpenFolder(dir);
            };
            card.Controls.Add(backup);

            Label tip = UiTheme.MakeLabel("首次使用若收不到数据，请在 Windows 防火墙中允许 jtdx、GridTracker2、qlog 的 UDP 入站。", 8.3f, UiTheme.TextFaint);
            tip.Location = new Point(20, 74);
            card.Controls.Add(tip);

            card.Resize += delegate { text.Width = Math.Max(400, card.ClientSize.Width - 40); };
            return card;
        }

        private void AddCardTitle(CardPanel card, string title, string subtitle)
        {
            Label titleLabel = UiTheme.MakeBoldLabel(title, 12f, UiTheme.Text);
            titleLabel.Location = new Point(20, 14);
            card.Controls.Add(titleLabel);

            Label sub = new Label();
            sub.AutoSize = false;
            sub.Text = subtitle;
            sub.Font = UiTheme.Font(9f);
            sub.ForeColor = UiTheme.TextMuted;
            sub.BackColor = Color.White;
            sub.Location = new Point(20, 40);
            sub.Height = 20;
            sub.Width = 800;
            card.Controls.Add(sub);
            card.Resize += delegate { sub.Width = Math.Max(400, card.ClientSize.Width - 40); };
        }

        private Label AddPathLabel(CardPanel card)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.AutoEllipsis = true;
            label.Font = UiTheme.Font(8.2f);
            label.ForeColor = UiTheme.TextFaint;
            label.BackColor = Color.White;
            label.Location = new Point(20, 62);
            label.Height = 18;
            label.Width = 700;
            card.Controls.Add(label);
            card.Resize += delegate { label.Width = Math.Max(400, card.ClientSize.Width - 40); };
            return label;
        }

        private Label AddCurrentLabel(CardPanel card)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.AutoEllipsis = true;
            label.Font = UiTheme.Font(8.7f);
            label.ForeColor = UiTheme.Text;
            label.BackColor = Color.White;
            label.Location = new Point(20, 84);
            label.Height = 20;
            label.Width = 760;
            card.Controls.Add(label);
            card.Resize += delegate { label.Width = Math.Max(400, card.ClientSize.Width - 40); };
            return label;
        }

        private void AddFieldLabel(CardPanel card, string text, int x, int y)
        {
            Label label = UiTheme.MakeLabel(text, 8.8f, UiTheme.TextMuted);
            label.Location = new Point(x, y);
            card.Controls.Add(label);
        }

        private TextBox AddTextBox(CardPanel card, int x, int y, int width)
        {
            TextBox box = new TextBox();
            box.Location = new Point(x, y);
            box.Width = width;
            box.Font = UiTheme.Font(9f);
            box.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(box);
            return box;
        }

        private NumericUpDown AddPortBox(CardPanel card, int x, int y, int width, int value)
        {
            NumericUpDown box = new NumericUpDown();
            box.Location = new Point(x, y);
            box.Width = width;
            box.Minimum = 1;
            box.Maximum = 65535;
            box.Value = value;
            box.Font = UiTheme.Font(9f);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.TextAlign = HorizontalAlignment.Right;
            card.Controls.Add(box);
            return box;
        }

        private void AddHint(CardPanel card, string text, int y)
        {
            Label hint = new Label();
            hint.AutoSize = false;
            hint.AutoEllipsis = true;
            hint.Text = text;
            hint.Font = UiTheme.Font(8.3f);
            hint.ForeColor = UiTheme.TextFaint;
            hint.BackColor = Color.White;
            hint.Location = new Point(20, y);
            hint.Height = 18;
            hint.Width = 800;
            card.Controls.Add(hint);
            card.Resize += delegate { hint.Width = Math.Max(400, card.ClientSize.Width - 40); };
        }

        private void AnchorButtonsRight(CardPanel card, int top, params Button[] buttons)
        {
            card.Resize += delegate
            {
                int x = card.ClientSize.Width - 20;
                for (int i = buttons.Length - 1; i >= 0; i--)
                {
                    x -= buttons[i].Width;
                    buttons[i].Location = new Point(x, top);
                    x -= 8;
                }
            };
        }

        private void FillUdpFields()
        {
            UdpSettings u = _settings.Udp;
            _jtdxAddr1.Text = u.JtdxUdp1Addr;
            _jtdxPort1.Value = Clamp(u.JtdxUdp1Port, 1, 65535);
            _jtdxAddr2.Text = u.JtdxUdp2Addr;
            _jtdxPort2.Value = Clamp(u.JtdxUdp2Port, 1, 65535);
            _jtdxAccept.Checked = u.JtdxAcceptRequests;

            _gt2Addr.Text = u.Gt2Addr;
            _gt2Port.Value = Clamp(u.Gt2Port, 1, 65535);
            _gt2Multicast.Checked = u.Gt2Multicast;

            _qlogPort.Value = Clamp(u.QlogPort, 1, 65535);
            _qlogAddr.Text = u.QlogAddr;
            _qlogJoin.Checked = u.QlogJoin;
            _qlogTtl.Value = Clamp(u.QlogTtl, 0, 255);
            _qlogForward.Text = u.QlogForwardAddr;
        }

        private void CommitUdpFields()
        {
            if (_settings == null || _jtdxAddr1 == null) return;
            UdpSettings u = _settings.Udp;
            u.JtdxUdp1Addr = _jtdxAddr1.Text.Trim();
            u.JtdxUdp1Port = (int)_jtdxPort1.Value;
            u.JtdxUdp2Addr = _jtdxAddr2.Text.Trim();
            u.JtdxUdp2Port = (int)_jtdxPort2.Value;
            u.JtdxAcceptRequests = _jtdxAccept.Checked;

            u.Gt2Addr = _gt2Addr.Text.Trim();
            u.Gt2Port = (int)_gt2Port.Value;
            u.Gt2Multicast = _gt2Multicast.Checked;

            u.QlogPort = (int)_qlogPort.Value;
            u.QlogAddr = _qlogAddr.Text.Trim();
            u.QlogJoin = _qlogJoin.Checked;
            u.QlogTtl = (int)_qlogTtl.Value;
            u.QlogForwardAddr = _qlogForward.Text.Trim();

            TrySave();
        }

        private void RefreshUdpState(bool loadFields)
        {
            _udpSnapshot = UdpConfig.ReadAll();
            if (loadFields) FillUdpFields();

            JtdxUdpState j = _udpSnapshot.Jtdx;
            _jtdxPath.Text = "配置：" + j.Path;
            if (j.Exists)
                _jtdxCurrent.Text = string.Format("当前：UDP1 {0}:{1}   ·   UDP2 {2}:{3}   ·   接受 UDP 请求 {4}",
                    j.Udp1Addr, j.Udp1Port, j.Udp2Addr, j.Udp2Port, j.AcceptRequests ? "开" : "关");
            else
                _jtdxCurrent.Text = string.IsNullOrEmpty(j.Error) ? "未找到 JTDX 配置文件" : "读取失败：" + j.Error;

            Gt2UdpState g = _udpSnapshot.Gt2;
            _gt2Path.Text = "配置：" + g.Path;
            if (g.Exists)
                _gt2Current.Text = string.Format("当前：WSJT-X {0}:{1}   ·   组播接收 {2}",
                    g.WsjtAddr, g.WsjtPort, g.Multicast ? "开" : "关");
            else
                _gt2Current.Text = string.IsNullOrEmpty(g.Error) ? "未找到 GridTracker2 配置文件" : "读取失败：" + g.Error;

            QlogUdpState q = _udpSnapshot.Qlog;
            _qlogPath.Text = "配置：" + q.Path;
            if (q.Exists)
                _qlogCurrent.Text = string.Format("当前：监听 {0}:{1}   ·   加入组播 {2}   ·   TTL {3}   ·   转发 {4}",
                    q.MulticastAddr, q.Port, q.Join ? "开" : "关", q.Ttl, q.ForwardAddr);
            else
                _qlogCurrent.Text = string.IsNullOrEmpty(q.Error) ? "未找到 QLog 数据库" : "读取失败：" + q.Error;

            UdpSettings desired = _settings.Udp;
            _udpListeners.Text = UdpConfig.DescribeListeners(desired.JtdxUdp1Port);

            if (!desired.Enabled)
            {
                _udpOverallTitle.Text = "UDP 配置检查已关闭";
                _udpOverallTitle.ForeColor = UiTheme.TextMuted;
                _udpOverallDetail.Text = "启动时不会检查或提示 UDP 配置差异；仍可在本页手动应用推荐配置。";
                _udpOverallDetail.ForeColor = UiTheme.TextMuted;
                return;
            }

            List<string> issues = _udpSnapshot.Mismatches(desired);
            if (issues.Count == 0)
            {
                _udpOverallTitle.Text = "UDP 配置一致，可以联动";
                _udpOverallTitle.ForeColor = UiTheme.Green;
                _udpOverallDetail.Text = "JTDX、GridTracker2、QLog 的端口与组播地址均与下方设置一致。";
                _udpOverallDetail.ForeColor = UiTheme.TextMuted;
            }
            else
            {
                _udpOverallTitle.Text = string.Format("检测到 {0} 处与设置不一致", issues.Count);
                _udpOverallTitle.ForeColor = UiTheme.Amber;
                _udpOverallDetail.Text = string.Join("；", issues.ToArray());
                _udpOverallDetail.ForeColor = UiTheme.TextMuted;
            }
        }

        private void ReadJtdxToFields()
        {
            _udpSnapshot = UdpConfig.ReadAll();
            JtdxUdpState j = _udpSnapshot.Jtdx;
            if (!j.Exists)
            {
                MessageBox.Show(this, "未找到 JTDX 配置文件：\n" + UdpConfig.JtdxIniPath, "读取失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _jtdxAddr1.Text = j.Udp1Addr;
            _jtdxPort1.Value = Clamp(j.Udp1Port, 1, 65535);
            _jtdxAddr2.Text = j.Udp2Addr;
            _jtdxPort2.Value = Clamp(j.Udp2Port, 1, 65535);
            _jtdxAccept.Checked = j.AcceptRequests;
            AppendLog("已读取 JTDX 当前 UDP 配置到表单（尚未写入）。", LogKind.Info);
        }

        private void ReadGt2ToFields()
        {
            _udpSnapshot = UdpConfig.ReadAll();
            Gt2UdpState g = _udpSnapshot.Gt2;
            if (!g.Exists)
            {
                MessageBox.Show(this, "未找到 GridTracker2 配置文件：\n" + UdpConfig.Gt2JsonPath, "读取失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _gt2Addr.Text = g.WsjtAddr;
            _gt2Port.Value = Clamp(g.WsjtPort, 1, 65535);
            _gt2Multicast.Checked = g.Multicast;
            AppendLog("已读取 GridTracker2 当前 UDP 配置到表单（尚未写入）。", LogKind.Info);
        }

        private void ReadQlogToFields()
        {
            _udpSnapshot = UdpConfig.ReadAll();
            QlogUdpState q = _udpSnapshot.Qlog;
            if (!q.Exists)
            {
                MessageBox.Show(this, "未找到 QLog 数据库：\n" + UdpConfig.QlogDbPath, "读取失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _qlogPort.Value = Clamp(q.Port, 1, 65535);
            _qlogAddr.Text = q.MulticastAddr;
            _qlogJoin.Checked = q.Join;
            _qlogTtl.Value = Clamp(q.Ttl, 0, 255);
            _qlogForward.Text = q.ForwardAddr;
            AppendLog("已读取 QLog 当前 UDP 配置到表单（尚未写入）。", LogKind.Info);
        }

        private bool ValidateUdpFields(out string error)
        {
            error = null;
            if (!IsValidAddress(_jtdxAddr1.Text)) { error = "JTDX UDP1 地址格式不正确。"; return false; }
            if (!IsValidAddress(_jtdxAddr2.Text)) { error = "JTDX UDP2 地址格式不正确。"; return false; }
            if (!IsValidAddress(_gt2Addr.Text)) { error = "GridTracker2 WSJT-X 地址格式不正确。"; return false; }
            if (!IsValidAddress(_qlogAddr.Text)) { error = "QLog 组播地址格式不正确。"; return false; }
            if (!IsValidAddress(_qlogForward.Text)) { error = "QLog 转发地址格式不正确。"; return false; }
            return true;
        }

        private static bool IsValidAddress(string text)
        {
            IPAddress address;
            if (!IPAddress.TryParse((text ?? "").Trim(), out address)) return false;
            return address.AddressFamily == AddressFamily.InterNetwork && !address.Equals(IPAddress.Any);
        }

        private void ApplyJtdx()
        {
            string error;
            if (!ValidateUdpFields(out error))
            {
                MessageBox.Show(this, error, "地址格式错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CommitUdpFields();

            AppEntry jtdx = _settings.FindApp("jtdx");
            if (jtdx != null && ProcessUtil.IsRunning(jtdx))
            {
                MessageBox.Show(this, "JTDX 正在运行，请先关闭 JTDX 再写入配置（避免退出时覆盖）。", "请先关闭 JTDX",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string backupDir = UdpConfig.CreateBackupDir(_settings);
                UdpConfig.ApplyJtdx(_settings.Udp, backupDir);
                AppendLog("JTDX UDP 配置已写入，备份：" + backupDir, LogKind.Success);
                RefreshUdpState(false);
                MessageBox.Show(this, "JTDX 的 UDP 配置已写入。\n备份目录：\n" + backupDir, "应用成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("写入 JTDX 配置失败：" + ex.Message, LogKind.Error);
                MessageBox.Show(this, ex.Message, "写入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyGt2()
        {
            string error;
            if (!ValidateUdpFields(out error))
            {
                MessageBox.Show(this, error, "地址格式错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CommitUdpFields();

            if (ProcessUtil.IsProcessNameRunning("GridTracker2"))
            {
                MessageBox.Show(this, "GridTracker2 正在运行，请先关闭它再写入配置。", "请先关闭 GridTracker2",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string backupDir = UdpConfig.CreateBackupDir(_settings);
                UdpConfig.ApplyGt2(_settings.Udp, backupDir);
                AppendLog("GridTracker2 UDP 配置已写入，备份：" + backupDir, LogKind.Success);
                RefreshUdpState(false);
                MessageBox.Show(this, "GridTracker2 的 UDP 配置已写入。\n备份目录：\n" + backupDir, "应用成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("写入 GridTracker2 配置失败：" + ex.Message, LogKind.Error);
                MessageBox.Show(this, ex.Message, "写入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyQlog()
        {
            string error;
            if (!ValidateUdpFields(out error))
            {
                MessageBox.Show(this, error, "地址格式错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CommitUdpFields();

            if (ProcessUtil.IsProcessNameRunning("qlog"))
            {
                MessageBox.Show(this, "QLog 正在运行，请先关闭 QLog 再写入配置。", "请先关闭 QLog",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AppendLog("正在备份并写入 QLog 数据库（约 58 MB），请稍候…", LogKind.Info);
                string backupDir = UdpConfig.CreateBackupDir(_settings);
                UdpConfig.ApplyQlog(_settings.Udp, backupDir);
                AppendLog("QLog UDP 配置已写入，备份：" + backupDir, LogKind.Success);
                RefreshUdpState(false);
                MessageBox.Show(this, "QLog 的 UDP 配置已写入。\n备份目录：\n" + backupDir, "应用成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("写入 QLog 配置失败：" + ex.Message, LogKind.Error);
                MessageBox.Show(this, ex.Message, "写入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyAllUdp()
        {
            string error;
            if (!ValidateUdpFields(out error))
            {
                MessageBox.Show(this, error, "地址格式错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CommitUdpFields();

            UdpSettings u = _settings.Udp;
            if (u.JtdxUdp1Port != u.Gt2Port || u.JtdxUdp1Port != u.QlogPort)
            {
                DialogResult result = MessageBox.Show(this,
                    "JTDX、GridTracker2、QLog 的 UDP 端口不一致，联动可能失败。\n仍然要写入吗？",
                    "端口不一致", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }

            List<string> running = new List<string>();
            AppEntry jtdx = _settings.FindApp("jtdx");
            if (jtdx != null && ProcessUtil.IsRunning(jtdx)) running.Add("JTDX");
            if (ProcessUtil.IsProcessNameRunning("GridTracker2")) running.Add("GridTracker2");
            if (ProcessUtil.IsProcessNameRunning("qlog")) running.Add("QLog");
            if (running.Count > 0)
            {
                MessageBox.Show(this,
                    "请先关闭以下软件，再一键写入配置：\n\n· " + string.Join("\n· ", running.ToArray()) +
                    "\n\n软件运行时会锁定或覆盖配置文件。",
                    "请先关闭软件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "将写入以下三套软件的 UDP 配置，并自动备份原文件：\n\n" +
                string.Format("· JTDX：{0}:{1} / {2}:{3}\n", u.JtdxUdp1Addr, u.JtdxUdp1Port, u.JtdxUdp2Addr, u.JtdxUdp2Port) +
                string.Format("· GridTracker2：{0}:{1}\n", u.Gt2Addr, u.Gt2Port) +
                string.Format("· QLog：{0}:{1}\n\n", u.QlogAddr, u.QlogPort) +
                "QLog 数据库约 58 MB，备份可能需要几秒钟。是否继续？",
                "一键应用推荐配置", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            string backupDir = UdpConfig.CreateBackupDir(_settings);
            List<string> errors = new List<string>();
            int ok = 0;

            AppendLog("开始写入三套软件的 UDP 配置，备份目录：" + backupDir, LogKind.Step);

            try
            {
                UdpConfig.ApplyJtdx(u, backupDir);
                ok++;
                AppendLog("  JTDX 配置已写入", LogKind.Success);
            }
            catch (Exception ex)
            {
                errors.Add("JTDX：" + ex.Message);
                AppendLog("  JTDX 写入失败：" + ex.Message, LogKind.Error);
            }

            try
            {
                UdpConfig.ApplyGt2(u, backupDir);
                ok++;
                AppendLog("  GridTracker2 配置已写入", LogKind.Success);
            }
            catch (Exception ex)
            {
                errors.Add("GridTracker2：" + ex.Message);
                AppendLog("  GridTracker2 写入失败：" + ex.Message, LogKind.Error);
            }

            try
            {
                AppendLog("  正在备份并写入 QLog 数据库（约 58 MB）…", LogKind.Info);
                UdpConfig.ApplyQlog(u, backupDir);
                ok++;
                AppendLog("  QLog 配置已写入", LogKind.Success);
            }
            catch (Exception ex)
            {
                errors.Add("QLog：" + ex.Message);
                AppendLog("  QLog 写入失败：" + ex.Message, LogKind.Error);
            }

            RefreshUdpState(false);

            if (errors.Count == 0)
            {
                MessageBox.Show(this,
                    "三套软件的 UDP 配置已全部写入并校验通过。\n\n备份目录：\n" + backupDir,
                    "应用完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    string.Format("成功 {0} 项，失败 {1} 项：\n\n", ok, errors.Count) +
                    string.Join("\n", errors.ToArray()) +
                    "\n\n备份目录：\n" + backupDir,
                    "部分配置未写入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
