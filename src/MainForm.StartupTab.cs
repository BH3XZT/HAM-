using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HamStartup
{
    public partial class MainForm
    {
        private ListView _list;
        private ComboBox _schemeCombo;
        private Label _schemeHint;
        private bool _loadingSchemes;
        private Label _detailName;
        private Label _detailMeta;
        private Label _detailStatus;
        private TextBox _pathBox;
        private TextBox _argsBox;
        private NumericUpDown _waitSeconds;
        private NumericUpDown _delaySeconds;
        private CheckBox _chkWaitWindow;
        private CheckBox _chkAdmin;
        private Button _btnBrowse;
        private Button _btnAutoDetect;
        private Button _btnUp;
        private Button _btnDown;
        private Button _btnStart;
        private Button _btnStop;
        private CheckBox _chkOptionsWait;
        private CheckBox _chkOptionsSkip;
        private CheckBox _chkOptionsFocus;
        private CheckBox _chkOptionsClose;
        private CheckBox _chkOptionsAllAtOnce;

        private bool _updatingDetail;
        private bool _loadingList;
        private SplitContainer _startupSplit;
        private bool _startupSplitInitialized;
        private int _dragSourceIndex = -1;

        private void BuildStartupTab(Panel page)
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = UiTheme.Bg;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            page.Controls.Add(root);

            root.Controls.Add(BuildSchemeBar(), 0, 0);
            root.Controls.Add(BuildStartupToolbar(), 0, 1);

            _startupContentHost = new Panel();
            _startupContentHost.Dock = DockStyle.Fill;
            _startupContentHost.BackColor = UiTheme.Bg;
            _startupClassicContent = BuildStartupContent();
            _startupClassicContent.Dock = DockStyle.Fill;
            _startupCardView = BuildStartupCardView();
            _startupCardView.Visible = false;
            _startupContentHost.Controls.Add(_startupClassicContent);
            _startupContentHost.Controls.Add(_startupCardView);
            root.Controls.Add(_startupContentHost, 0, 2);
            root.Controls.Add(BuildActionBar(), 0, 3);
        }

        private Panel BuildSchemeBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Color.White;
            bar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(UiTheme.Border))
                    e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            Label label = UiTheme.MakeBoldLabel("启动方案", 9f, UiTheme.Text);
            label.Location = new Point(10, 8);
            bar.Controls.Add(label);

            _schemeCombo = new ComboBox();
            _schemeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _schemeCombo.Font = UiTheme.Font(9.5f);
            _schemeCombo.Location = new Point(78, 2);
            _schemeCombo.Width = 210;
            _schemeCombo.SelectedIndexChanged += SchemeComboChanged;
            bar.Controls.Add(_schemeCombo);

            Button create = UiTheme.SmallButton("新建", 56, 28);
            create.Location = new Point(296, 1);
            create.Click += delegate { CreateScheme(); };
            bar.Controls.Add(create);

            Button copy = UiTheme.SmallButton("复制", 56, 28);
            copy.Location = new Point(356, 1);
            copy.Click += delegate { CopyScheme(); };
            bar.Controls.Add(copy);

            Button rename = UiTheme.SmallButton("重命名", 64, 28);
            rename.Location = new Point(416, 1);
            rename.Click += delegate { RenameScheme(); };
            bar.Controls.Add(rename);

            Button delete = UiTheme.SmallButton("删除", 56, 28);
            delete.Location = new Point(484, 1);
            delete.Click += delegate { DeleteScheme(); };
            bar.Controls.Add(delete);

            _schemeHint = UiTheme.MakeLabel("方案可以自由搭配任意 Ham 软件（如 N1MM、Log4OM、MSHV、JS8Call 等）", 8.3f, UiTheme.TextFaint);
            _schemeHint.Location = new Point(552, 8);
            bar.Controls.Add(_schemeHint);

            bar.Resize += delegate
            {
                if (bar.Width > 900) _schemeHint.Left = Math.Min(552, bar.Width - _schemeHint.Width - 10);
                else _schemeHint.Visible = false;
            };
            return bar;
        }

        private void RefreshSchemeCombo()
        {
            if (_schemeCombo == null) return;
            _loadingSchemes = true;
            _schemeCombo.Items.Clear();
            int selected = 0;
            for (int i = 0; i < _settings.Schemes.Count; i++)
            {
                Scheme scheme = _settings.Schemes[i];
                _schemeCombo.Items.Add(scheme.Name);
                if (string.Equals(scheme.Id, _settings.ActiveSchemeId, StringComparison.OrdinalIgnoreCase))
                    selected = i;
            }
            if (_schemeCombo.Items.Count > 0) _schemeCombo.SelectedIndex = selected;
            _loadingSchemes = false;
        }

        private void SchemeComboChanged(object sender, EventArgs e)
        {
            if (_loadingSchemes || _schemeCombo.SelectedIndex < 0) return;
            Scheme scheme = _settings.Schemes[_schemeCombo.SelectedIndex];
            if (scheme.Id == _settings.ActiveSchemeId) return;
            _settings.ActiveSchemeId = scheme.Id;
            TrySave();
            BuildAppList();
            AppendLog("已切换到启动方案：" + scheme.Name, LogKind.Info);
        }

        private void CreateScheme()
        {
            string name = InputDialog.Show(this, "新建方案", "方案名称", "新方案");
            if (string.IsNullOrEmpty(name)) return;
            Scheme scheme = new Scheme();
            scheme.Id = _settings.NewSchemeId();
            scheme.Name = name.Trim();
            // Start from an empty scheme; the user adds the software they want.
            _settings.Schemes.Add(scheme);
            _settings.ActiveSchemeId = scheme.Id;
            TrySave();
            RefreshSchemeCombo();
            BuildAppList();
            AppendLog("已新建方案：" + scheme.Name + "（请点“添加软件”加入程序）", LogKind.Success);
        }

        private void CopyScheme()
        {
            Scheme source = _settings.ActiveScheme;
            string name = InputDialog.Show(this, "复制方案", "新方案名称", source.Name + " 副本");
            if (string.IsNullOrEmpty(name)) return;

            Scheme scheme = new Scheme();
            scheme.Id = _settings.NewSchemeId();
            scheme.Name = name.Trim();
            foreach (AppEntry app in source.Apps)
            {
                AppEntry copy = new AppEntry();
                copy.Id = app.Id;
                copy.Name = app.Name;
                copy.ExePath = app.ExePath;
                copy.Arguments = app.Arguments;
                copy.Enabled = app.Enabled;
                copy.WaitForWindow = app.WaitForWindow;
                copy.WaitSeconds = app.WaitSeconds;
                copy.DelaySeconds = app.DelaySeconds;
                copy.RunAsAdmin = app.RunAsAdmin;
                scheme.Apps.Add(copy);
            }
            _settings.Schemes.Add(scheme);
            _settings.ActiveSchemeId = scheme.Id;
            TrySave();
            RefreshSchemeCombo();
            BuildAppList();
            AppendLog("已复制方案：" + scheme.Name, LogKind.Success);
        }

        private void RenameScheme()
        {
            Scheme scheme = _settings.ActiveScheme;
            string name = InputDialog.Show(this, "重命名方案", "方案名称", scheme.Name);
            if (string.IsNullOrEmpty(name)) return;
            scheme.Name = name.Trim();
            TrySave();
            RefreshSchemeCombo();
            AppendLog("方案已重命名为：" + scheme.Name, LogKind.Info);
        }

        private void DeleteScheme()
        {
            if (_settings.Schemes.Count <= 1)
            {
                MessageBox.Show(this, "至少保留一个启动方案。", "删除方案",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Scheme scheme = _settings.ActiveScheme;
            DialogResult result = MessageBox.Show(this,
                "确定删除方案“" + scheme.Name + "”吗？\n该方案中的软件列表会一起删除。",
                "删除方案", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            _settings.Schemes.Remove(scheme);
            _settings.ActiveSchemeId = _settings.Schemes[0].Id;
            TrySave();
            RefreshSchemeCombo();
            BuildAppList();
            AppendLog("已删除方案：" + scheme.Name, LogKind.Warn);
        }

        private Panel BuildStartupToolbar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = UiTheme.Bg;

            Label title = UiTheme.MakeBoldLabel("① 选择软件与启动顺序", 11.5f, UiTheme.Text);
            title.Location = new Point(0, 0);
            bar.Controls.Add(title);

            Label hint = UiTheme.MakeLabel("勾选参与启动的软件；可直接拖动列表行调整先后；启动顺序自上而下执行。", 8.8f, UiTheme.TextMuted);
            hint.Location = new Point(0, 24);
            bar.Controls.Add(hint);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Right;
            tools.Width = 500;
            tools.FlowDirection = FlowDirection.RightToLeft;
            tools.WrapContents = false;
            tools.BackColor = Color.Transparent;
            tools.Padding = new Padding(0, 5, 0, 0);

            Button add = UiTheme.SmallButton("添加软件", 92, 30);
            add.Margin = new Padding(6, 0, 0, 0);
            add.Click += delegate { AddSoftwareClicked(); };
            tools.Controls.Add(add);

            Button remove = UiTheme.SmallButton("删除软件", 84, 30);
            remove.Margin = new Padding(6, 0, 0, 0);
            remove.Click += delegate { RemoveSoftwareClicked(); };
            tools.Controls.Add(remove);

            Button refresh = UiTheme.SmallButton("刷新状态", 92, 30);
            refresh.Margin = new Padding(6, 0, 0, 0);
            refresh.Click += delegate
            {
                RefreshStatus();
                AppendLog("已刷新运行状态。", LogKind.Info);
            };
            tools.Controls.Add(refresh);

            Button all = UiTheme.SmallButton("全部勾选", 84, 30);
            all.Margin = new Padding(6, 0, 0, 0);
            all.Click += delegate { SetAllEnabled(true); };
            tools.Controls.Add(all);

            Button none = UiTheme.SmallButton("全部不选", 84, 30);
            none.Margin = new Padding(6, 0, 0, 0);
            none.Click += delegate { SetAllEnabled(false); };
            tools.Controls.Add(none);

            bar.Controls.Add(tools);
            return bar;
        }

        private Control BuildStartupContent()
        {
            _startupSplit = new SplitContainer();
            _startupSplit.Dock = DockStyle.Fill;
            _startupSplit.Orientation = Orientation.Vertical;
            _startupSplit.SplitterWidth = 7;
            _startupSplit.Panel1MinSize = 0;
            _startupSplit.Panel2MinSize = 0;
            _startupSplit.BackColor = Color.FromArgb(190, 203, 220);
            if (_resizeTip != null) _resizeTip.SetToolTip(_startupSplit, "拖动分隔条调整左侧列表与右侧编辑区的宽度");

            _list = new ListView();
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.CheckBoxes = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.BackColor = Color.White;
            _list.ForeColor = UiTheme.Text;
            _list.Font = UiTheme.Font(9.5f);
            _list.ShowItemToolTips = true;
            _list.Margin = new Padding(0, 0, 12, 0);

            ImageList rowHeight = new ImageList();
            rowHeight.ImageSize = new Size(1, 30);
            rowHeight.ColorDepth = ColorDepth.Depth32Bit;
            rowHeight.Images.Add(new Bitmap(1, 30));
            _list.SmallImageList = rowHeight;

            _list.Columns.Add("顺序", 50, HorizontalAlignment.Left);
            _list.Columns.Add("软件", 120, HorizontalAlignment.Left);
            _list.Columns.Add("程序路径", 350, HorizontalAlignment.Left);
            _list.Columns.Add("状态", 92, HorizontalAlignment.Left);
            _list.Columns.Add("窗口+延时", 104, HorizontalAlignment.Left);

            _list.Resize += delegate { AdjustColumnWidths(); };
            _list.ItemChecked += List_ItemChecked;
            _list.SelectedIndexChanged += delegate { UpdateDetail(); };
            _list.DoubleClick += delegate
            {
                AppEntry app = SelectedApp();
                if (app != null && !string.IsNullOrEmpty(app.Directory)) ProcessUtil.OpenFolder(app.Directory);
            };

            _list.AllowDrop = true;
            _list.ItemDrag += delegate(object sender, ItemDragEventArgs e)
            {
                ListViewItem item = e.Item as ListViewItem;
                if (item == null) return;
                _dragSourceIndex = item.Index;
                _list.DoDragDrop(item, DragDropEffects.Move);
            };
            _list.DragEnter += delegate(object sender, DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(typeof(ListViewItem)) ? DragDropEffects.Move : DragDropEffects.None;
            };
            _list.DragOver += delegate(object sender, DragEventArgs e)
            {
                e.Effect = DragDropEffects.Move;
            };
            _list.DragDrop += delegate(object sender, DragEventArgs e)
            {
                if (_dragSourceIndex < 0) return;
                Point point = _list.PointToClient(new Point(e.X, e.Y));
                ListViewItem target = _list.GetItemAt(point.X, point.Y);
                int targetIndex = target == null ? _settings.Apps.Count - 1 : target.Index;
                MoveAppTo(_dragSourceIndex, targetIndex);
                _dragSourceIndex = -1;
            };

            _startupSplit.Panel1.Controls.Add(_list);
            _startupSplit.Panel2.Controls.Add(BuildDetailCard());
            return _startupSplit;
        }

        private Control BuildDetailCard()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.AutoScroll = true;

            Label caption = UiTheme.MakeBoldLabel("编辑选中项", 11f, UiTheme.Text);
            caption.Location = new Point(18, 10);
            card.Controls.Add(caption);

            _detailName = UiTheme.MakeBoldLabel("未选择", 14f, UiTheme.Text);
            _detailName.AutoSize = false;
            _detailName.Height = 26;
            _detailName.Location = new Point(18, 34);
            _detailName.AutoEllipsis = true;
            card.Controls.Add(_detailName);

            _detailMeta = new Label();
            _detailMeta.AutoSize = false;
            _detailMeta.AutoEllipsis = true;
            _detailMeta.Height = 18;
            _detailMeta.Font = UiTheme.Font(8.4f);
            _detailMeta.ForeColor = UiTheme.TextMuted;
            _detailMeta.BackColor = Color.White;
            _detailMeta.Location = new Point(18, 62);
            card.Controls.Add(_detailMeta);

            _detailStatus = UiTheme.MakeLabel("○ 未运行", 9f, UiTheme.TextFaint);
            _detailStatus.Location = new Point(18, 82);
            card.Controls.Add(_detailStatus);

            card.Controls.Add(DetailLabel("程序路径（exe / 桌面快捷方式）", 108));

            _pathBox = new TextBox();
            _pathBox.Location = new Point(18, 124);
            _pathBox.Height = 27;
            _pathBox.Font = UiTheme.Font(8.6f);
            _pathBox.BorderStyle = BorderStyle.FixedSingle;
            _pathBox.Leave += delegate { CommitPathFromBox(); };
            _pathBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { CommitPathFromBox(); e.SuppressKeyPress = true; }
            };
            card.Controls.Add(_pathBox);

            _btnBrowse = UiTheme.SmallButton("浏览", 62, 27);
            _btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnBrowse.Top = 124;
            _btnBrowse.Click += BrowseForExe;
            card.Controls.Add(_btnBrowse);

            card.Controls.Add(DetailLabel("启动参数（可选）", 160));

            _argsBox = new TextBox();
            _argsBox.Location = new Point(18, 176);
            _argsBox.Font = UiTheme.Font(9f);
            _argsBox.BorderStyle = BorderStyle.FixedSingle;
            _argsBox.Leave += delegate { CommitArgsFromBox(); };
            card.Controls.Add(_argsBox);

            _chkWaitWindow = new CheckBox();
            _chkWaitWindow.Text = "等待主窗口出现";
            _chkWaitWindow.Location = new Point(18, 214);
            _chkWaitWindow.AutoSize = true;
            _chkWaitWindow.Font = UiTheme.Font(9f);
            _chkWaitWindow.BackColor = Color.White;
            _chkWaitWindow.CheckedChanged += delegate { CommitDetailFields(); };
            card.Controls.Add(_chkWaitWindow);

            _waitSeconds = new NumericUpDown();
            _waitSeconds.Location = new Point(150, 212);
            _waitSeconds.Width = 58;
            _waitSeconds.Minimum = 1;
            _waitSeconds.Maximum = 300;
            _waitSeconds.Font = UiTheme.Font(9f);
            _waitSeconds.BorderStyle = BorderStyle.FixedSingle;
            _waitSeconds.TextAlign = HorizontalAlignment.Right;
            _waitSeconds.ValueChanged += delegate { CommitDetailFields(); };
            card.Controls.Add(_waitSeconds);

            Label sec1 = UiTheme.MakeLabel("秒内", 8.8f, UiTheme.TextMuted);
            sec1.Location = new Point(214, 216);
            card.Controls.Add(sec1);

            Label delayLabel = UiTheme.MakeLabel("启动后额外等待", 9f, UiTheme.Text);
            delayLabel.Location = new Point(18, 248);
            card.Controls.Add(delayLabel);

            _delaySeconds = new NumericUpDown();
            _delaySeconds.Location = new Point(150, 244);
            _delaySeconds.Width = 58;
            _delaySeconds.Minimum = 0;
            _delaySeconds.Maximum = 300;
            _delaySeconds.Font = UiTheme.Font(9f);
            _delaySeconds.BorderStyle = BorderStyle.FixedSingle;
            _delaySeconds.TextAlign = HorizontalAlignment.Right;
            _delaySeconds.ValueChanged += delegate { CommitDetailFields(); };
            card.Controls.Add(_delaySeconds);

            Label sec2 = UiTheme.MakeLabel("秒", 8.8f, UiTheme.TextMuted);
            sec2.Location = new Point(214, 248);
            card.Controls.Add(sec2);

            _chkAdmin = new CheckBox();
            _chkAdmin.Text = "以管理员身份启动（会弹出 UAC）";
            _chkAdmin.Location = new Point(18, 276);
            _chkAdmin.AutoSize = true;
            _chkAdmin.Font = UiTheme.Font(9f);
            _chkAdmin.BackColor = Color.White;
            _chkAdmin.CheckedChanged += delegate { CommitDetailFields(); };
            card.Controls.Add(_chkAdmin);

            _btnUp = UiTheme.SmallButton("↑ 上移", 56, 30);
            _btnUp.Location = new Point(18, 306);
            _btnUp.Click += delegate { MoveSelected(-1); };
            card.Controls.Add(_btnUp);

            _btnDown = UiTheme.SmallButton("↓ 下移", 56, 30);
            _btnDown.Location = new Point(80, 306);
            _btnDown.Click += delegate { MoveSelected(1); };
            card.Controls.Add(_btnDown);

            _btnAutoDetect = UiTheme.SmallButton("自动检测", 88, 30);
            _btnAutoDetect.Location = new Point(142, 306);
            _btnAutoDetect.Click += AutoDetectClicked;
            card.Controls.Add(_btnAutoDetect);

            Button desktop = UiTheme.SmallButton("桌面图标", 80, 30);
            desktop.Location = new Point(236, 306);
            desktop.Click += UseDesktopShortcutClicked;
            card.Controls.Add(desktop);

            card.Resize += delegate
            {
                int width = card.ClientSize.Width;
                _pathBox.Width = Math.Max(110, width - 18 - 62 - 8 - 18);
                _btnBrowse.Left = width - 18 - 62;
                _argsBox.Width = Math.Max(110, width - 36);
                _detailName.Width = Math.Max(100, width - 36);
                _detailMeta.Width = Math.Max(100, width - 36);
            };

            return card;
        }

        private Label DetailLabel(string text, int y)
        {
            Label label = UiTheme.MakeLabel(text, 8.5f, UiTheme.TextMuted);
            label.Location = new Point(18, y);
            return label;
        }

        private Control BuildActionBar()
        {
            CardPanel bar = new CardPanel();
            bar.Dock = DockStyle.Fill;

            _btnStart = UiTheme.PrimaryButton("▶  按顺序启动", 178, 44);
            _btnStart.Location = new Point(18, 8);
            _btnStart.Click += delegate { StartSequence(); };
            bar.Controls.Add(_btnStart);

            _btnStop = UiTheme.SecondaryButton("停止全部", 112, 44);
            _btnStop.Location = new Point(208, 8);
            _btnStop.Enabled = false;
            _btnStop.Click += delegate { StopSequence(); };
            bar.Controls.Add(_btnStop);

            _runHint = UiTheme.MakeLabel("", 9f, UiTheme.TextMuted);
            _runHint.Location = new Point(338, 22);
            bar.Controls.Add(_runHint);

            _chkOptionsWait = OptionCheck("等待窗口就绪", 18, 56);
            _chkOptionsWait.Checked = _settings.WaitForWindows;
            _chkOptionsWait.CheckedChanged += delegate
            {
                _settings.WaitForWindows = _chkOptionsWait.Checked;
                TrySave();
            };
            bar.Controls.Add(_chkOptionsWait);

            _chkOptionsSkip = OptionCheck("已运行则跳过", 158, 56);
            _chkOptionsSkip.Checked = _settings.SkipRunning;
            _chkOptionsSkip.CheckedChanged += delegate
            {
                _settings.SkipRunning = _chkOptionsSkip.Checked;
                TrySave();
            };
            bar.Controls.Add(_chkOptionsSkip);

            _chkOptionsFocus = OptionCheck("完成后前置 JTDX", 300, 56);
            _chkOptionsFocus.Checked = _settings.FocusJtdx;
            _chkOptionsFocus.CheckedChanged += delegate
            {
                _settings.FocusJtdx = _chkOptionsFocus.Checked;
                TrySave();
            };
            bar.Controls.Add(_chkOptionsFocus);

            _chkOptionsClose = OptionCheck("退出时关闭", 452, 56);
            _chkOptionsClose.Checked = _settings.CloseAppsOnExit;
            _chkOptionsClose.CheckedChanged += delegate
            {
                _settings.CloseAppsOnExit = _chkOptionsClose.Checked;
                TrySave();
            };
            bar.Controls.Add(_chkOptionsClose);

            _chkOptionsAllAtOnce = OptionCheck("全部同时启动（忽略顺序）", 600, 56);
            _chkOptionsAllAtOnce.Checked = _settings.LaunchAllAtOnce;
            _chkOptionsAllAtOnce.CheckedChanged += delegate
            {
                _settings.LaunchAllAtOnce = _chkOptionsAllAtOnce.Checked;
                _btnStart.Text = _settings.LaunchAllAtOnce ? "▶  全部同时启动" : "▶  按顺序启动";
                TrySave();
            };
            bar.Controls.Add(_chkOptionsAllAtOnce);
            _btnStart.Text = _settings.LaunchAllAtOnce ? "▶  全部同时启动" : "▶  按顺序启动";

            return bar;
        }

        private CheckBox OptionCheck(string text, int x, int y)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Location = new Point(x, y);
            box.AutoSize = true;
            box.Font = UiTheme.Font(8.8f);
            box.ForeColor = UiTheme.TextMuted;
            box.BackColor = Color.White;
            return box;
        }

        private void AddSoftwareClicked()
        {
            AddAppResult result = AddAppDialog.Show(this);
            if (result == null) return;

            foreach (AppEntry existing in _settings.Apps)
            {
                if (string.Equals(existing.ExePath, result.Path, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "该软件已经在当前方案中。", "添加软件",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            int n = 1;
            while (_settings.FindApp("custom_" + n) != null) n++;
            AppEntry app = new AppEntry();
            app.Id = "custom_" + n;
            app.Name = result.Name;
            app.ExePath = result.Path;
            app.Enabled = true;
            app.WaitForWindow = true;
            app.WaitSeconds = 30;
            app.DelaySeconds = 3;
            _settings.Apps.Add(app);
            TrySave();
            BuildAppList();
            for (int i = 0; i < _list.Items.Count; i++)
            {
                if (object.ReferenceEquals(_list.Items[i].Tag, app))
                {
                    _list.Items[i].Selected = true;
                    _list.Items[i].Focused = true;
                    break;
                }
            }
            AppendLog("已向方案“" + _settings.ActiveScheme.Name + "”添加软件：" + app.Name, LogKind.Success);
        }

        private void RemoveSoftwareClicked()
        {
            AppEntry app = SelectedApp();
            if (app == null)
            {
                MessageBox.Show(this, "请先在列表中选择要删除的软件。", "删除软件",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult result = MessageBox.Show(this,
                "从当前方案中删除“" + app.Name + "”？\n（不会卸载该软件，也不会删除它的配置）",
                "删除软件", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            _settings.Apps.Remove(app);
            TrySave();
            BuildAppList();
            AppendLog("已从方案中删除：" + app.Name, LogKind.Warn);
        }

        private void SetAllEnabled(bool enabled)
        {
            foreach (AppEntry app in _settings.Apps) app.Enabled = enabled;
            _loadingList = true;
            foreach (ListViewItem item in _list.Items) item.Checked = enabled;
            _loadingList = false;
            TrySave();
            UpdateRunHint();
        }

        private void BuildAppList()
        {
            _loadingList = true;
            _list.BeginUpdate();
            _list.Items.Clear();
            int index = 1;
            foreach (AppEntry app in _settings.Apps)
            {
                ListViewItem item = new ListViewItem(index.ToString());
                item.SubItems.Add(app.Name);
                item.SubItems.Add(string.IsNullOrEmpty(app.ExePath) ? "未检测到，请点击“自动检测”" : app.ExePath);
                item.SubItems.Add(ProcessUtil.IsRunning(app) ? "运行中" : "未运行");
                item.SubItems.Add(FormatWait(app));
                item.Tag = app;
                item.Checked = app.Enabled;
                item.ImageIndex = 0;
                item.ToolTipText = string.IsNullOrEmpty(app.ExePath)
                    ? "未设置程序路径"
                    : TooltipFor(app);
                _list.Items.Add(item);
                index++;
            }
            _list.EndUpdate();
            _loadingList = false;

            AdjustColumnWidths();
            if (_list.Items.Count > 0)
            {
                _list.Items[0].Selected = true;
                _list.Select();
            }
            UpdateDetail();
            if (_startupCardView != null) RefreshStartupCards();
        }

        private static string FormatWait(AppEntry app)
        {
            if (!app.WaitForWindow) return "延时 " + app.DelaySeconds + "s";
            return app.WaitSeconds + "s + " + app.DelaySeconds + "s";
        }

        private void AdjustColumnWidths()
        {
            if (_list == null || _list.Columns.Count < 5) return;
            int used = _list.Columns[0].Width + _list.Columns[1].Width +
                       _list.Columns[3].Width + _list.Columns[4].Width;
            int width = _list.ClientSize.Width - used - 4;
            _list.Columns[2].Width = Math.Max(180, width);
        }

        private void List_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loadingList) return;
            AppEntry app = e.Item.Tag as AppEntry;
            if (app == null) return;
            app.Enabled = e.Item.Checked;
            TrySave();
            UpdateRunHint();
        }

        private AppEntry SelectedApp()
        {
            if (_list == null || _list.SelectedItems.Count == 0) return null;
            return _list.SelectedItems[0].Tag as AppEntry;
        }

        private void UpdateDetail()
        {
            AppEntry app = SelectedApp();
            _updatingDetail = true;
            if (app == null)
            {
                _detailName.Text = "未选择";
                _detailMeta.Text = "";
                _detailStatus.Text = "○ 未运行";
                _pathBox.Text = "";
                _argsBox.Text = "";
                _waitSeconds.Value = 30;
                _delaySeconds.Value = 3;
                _chkWaitWindow.Checked = true;
                _chkAdmin.Checked = false;
                SetDetailEnabled(false);
            }
            else
            {
                SetDetailEnabled(true);
                _detailName.Text = app.Name;
                string description = AppCatalog.Description(app.Id);
                _detailMeta.Text = "进程：" + app.ProcessName +
                                   (app.IsShortcut ? " · 快捷方式" : "") +
                                   (string.IsNullOrEmpty(description) ? "" : "   ·   " + description);
                _pathBox.Text = app.ExePath ?? "";
                _argsBox.Text = app.Arguments ?? "";
                _chkWaitWindow.Checked = app.WaitForWindow;
                _waitSeconds.Value = Clamp(app.WaitSeconds, (int)_waitSeconds.Minimum, (int)_waitSeconds.Maximum);
                _delaySeconds.Value = Clamp(app.DelaySeconds, (int)_delaySeconds.Minimum, (int)_delaySeconds.Maximum);
                _chkAdmin.Checked = app.RunAsAdmin;
                _waitSeconds.Enabled = app.WaitForWindow;

                bool running = ProcessUtil.IsRunning(app);
                _detailStatus.Text = running ? "● 正在运行" : "○ 未运行";
                _detailStatus.ForeColor = running ? UiTheme.Green : UiTheme.TextFaint;
            }
            _updatingDetail = false;
        }

        private void SetDetailEnabled(bool enabled)
        {
            _pathBox.Enabled = enabled;
            _argsBox.Enabled = enabled;
            _chkWaitWindow.Enabled = enabled;
            _waitSeconds.Enabled = enabled;
            _delaySeconds.Enabled = enabled;
            _chkAdmin.Enabled = enabled;
            _btnBrowse.Enabled = enabled;
            _btnAutoDetect.Enabled = enabled;
            _btnUp.Enabled = enabled;
            _btnDown.Enabled = enabled;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void CommitDetailFields()
        {
            if (_updatingDetail) return;
            AppEntry app = SelectedApp();
            if (app == null) return;

            app.WaitForWindow = _chkWaitWindow.Checked;
            app.WaitSeconds = (int)_waitSeconds.Value;
            app.DelaySeconds = (int)_delaySeconds.Value;
            app.RunAsAdmin = _chkAdmin.Checked;
            _waitSeconds.Enabled = app.WaitForWindow;
            _chkWaitWindow.Enabled = true;

            UpdateRow(app);
            TrySave();
            UpdateRunHint();
        }

        private void CommitPathFromBox()
        {
            if (_updatingDetail) return;
            AppEntry app = SelectedApp();
            if (app == null) return;
            string path = (_pathBox.Text ?? "").Trim().Trim('"');
            if (path == app.ExePath) return;
            app.ExePath = path;
            UpdateRow(app);
            TrySave();
            if (path.Length > 0 && !File.Exists(path))
                AppendLog("路径不存在：" + path, LogKind.Warn);
        }

        private void CommitArgsFromBox()
        {
            if (_updatingDetail) return;
            AppEntry app = SelectedApp();
            if (app == null) return;
            app.Arguments = _argsBox.Text ?? "";
            TrySave();
        }

        private void UpdateRow(AppEntry app)
        {
            foreach (ListViewItem item in _list.Items)
            {
                if (!object.ReferenceEquals(item.Tag, app)) continue;
                item.SubItems[2].Text = string.IsNullOrEmpty(app.ExePath) ? "未检测到，请点击“自动检测”" : app.ExePath;
                item.SubItems[4].Text = FormatWait(app);
                item.ToolTipText = string.IsNullOrEmpty(app.ExePath)
                    ? "未设置程序路径"
                    : TooltipFor(app);

                bool exists = app.PathExists;
                item.SubItems[2].ForeColor = exists ? UiTheme.Text : UiTheme.Red;
                break;
            }
        }

        private static string TooltipFor(AppEntry app)
        {
            string text = app.ExePath + "\n" + AppCatalog.Description(app.Id);
            if (app.IsShortcut)
            {
                ShortcutInfo info = app.Shortcut;
                if (info != null && info.Valid)
                    text += "\n快捷方式目标：" + info.TargetPath + "\n起始目录：" + info.WorkingDirectory;
                else
                    text += "\n快捷方式无效：" + (info == null ? "无法解析" : info.Error);
            }
            return text;
        }

        private void MoveAppTo(int source, int target)
        {
            if (source < 0 || source >= _settings.Apps.Count) return;
            if (target < 0) target = 0;
            if (target >= _settings.Apps.Count) target = _settings.Apps.Count - 1;
            if (source == target) return;

            AppEntry app = _settings.Apps[source];
            _settings.Apps.RemoveAt(source);
            if (target > source) target--;
            if (target < 0) target = 0;
            if (target > _settings.Apps.Count) target = _settings.Apps.Count;
            _settings.Apps.Insert(target, app);

            BuildAppList();
            foreach (ListViewItem item in _list.Items)
            {
                if (object.ReferenceEquals(item.Tag, app))
                {
                    item.Selected = true;
                    item.Focused = true;
                    break;
                }
            }
            TrySave();
            AppendLog(string.Format("已拖动调整：{0} 移到第 {1} 位。", app.Name, target + 1), LogKind.Info);
        }

        private void MoveSelected(int direction)
        {
            if (_list.SelectedItems.Count == 0) return;
            int index = _list.SelectedItems[0].Index;
            int target = index + direction;
            if (target < 0 || target >= _settings.Apps.Count) return;

            AppEntry temp = _settings.Apps[index];
            _settings.Apps[index] = _settings.Apps[target];
            _settings.Apps[target] = temp;

            BuildAppList();
            _list.Items[target].Selected = true;
            _list.Items[target].Focused = true;
            TrySave();
            AppendLog(string.Format("已将 {0} 移到第 {1} 位。", temp.Name, target + 1), LogKind.Info);
        }

        private void BrowseForExe(object sender, EventArgs e)
        {
            AppEntry app = SelectedApp();
            if (app == null) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 " + app.Name + " 的程序或桌面快捷方式";
                dialog.Filter = "程序或快捷方式 (*.exe;*.lnk)|*.exe;*.lnk|程序 (*.exe)|*.exe|快捷方式 (*.lnk)|*.lnk|所有文件 (*.*)|*.*";
                string startDir = app.Directory;
                if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
                    dialog.InitialDirectory = startDir;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                _pathBox.Text = dialog.FileName;
                app.ExePath = dialog.FileName;
                UpdateRow(app);
                TrySave();
                RefreshStatus();
                if (app.IsShortcut)
                {
                    ShortcutInfo info = app.Shortcut;
                    AppendLog("已设置 " + app.Name + " 快捷方式：" + dialog.FileName, LogKind.Success);
                    if (info != null && info.Valid)
                        AppendLog("  快捷方式目标：" + info.TargetPath + "  [起始目录 " + info.WorkingDirectory + "]", LogKind.Info);
                    else
                        AppendLog("  快捷方式无法解析：" + (info == null ? "未知错误" : info.Error), LogKind.Warn);
                }
                else
                {
                    AppendLog("已设置 " + app.Name + "：" + dialog.FileName, LogKind.Success);
                }
            }
        }

        private void UseDesktopShortcutClicked(object sender, EventArgs e)
        {
            AppEntry app = SelectedApp();
            if (app == null) return;

            string lnk = null;
            try
            {
                lnk = ShortcutResolver.FindDesktopShortcut(
                    AppDetector.ShortcutKeyword(app.Id), AppCatalog.KnownExeName(app.Id));
            }
            catch { }

            if (string.IsNullOrEmpty(lnk))
            {
                MessageBox.Show(this,
                    "没有找到与 " + app.Name + " 匹配的桌面图标。\n\n" +
                    "请点击“浏览”，直接选择桌面上的 .lnk 快捷方式。",
                    "桌面图标", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            app.ExePath = lnk;
            _pathBox.Text = lnk;
            UpdateRow(app);
            TrySave();
            RefreshStatus();
            AppendLog("已改用 " + app.Name + " 桌面图标：" + lnk, LogKind.Success);
            ShortcutInfo info = app.Shortcut;
            if (info != null && info.Valid)
                AppendLog("  快捷方式目标：" + info.TargetPath + "  [起始目录 " + info.WorkingDirectory + "]", LogKind.Info);
        }

        private void AutoDetectClicked(object sender, EventArgs e)
        {
            AppEntry app = SelectedApp();
            if (app == null) return;

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            string found = null;
            try { found = AppDetector.Find(app, true); }
            catch { }
            finally { Cursor = previous; }

            if (string.IsNullOrEmpty(found))
            {
                MessageBox.Show(this, "没有自动找到 " + app.Name + "。\n请点击“浏览”手动选择 exe 文件。",
                    "自动检测", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            app.ExePath = found;
            _pathBox.Text = found;
            UpdateRow(app);
            TrySave();
            RefreshStatus();
            if (app.IsShortcut)
            {
                ShortcutInfo info = app.Shortcut;
                AppendLog("自动检测到 " + app.Name + " 桌面快捷方式：" + found, LogKind.Success);
                if (info != null && info.Valid)
                    AppendLog("  快捷方式目标：" + info.TargetPath + "  [起始目录 " + info.WorkingDirectory + "]", LogKind.Info);
            }
            else
            {
                AppendLog("自动检测到 " + app.Name + "：" + found, LogKind.Success);
            }
        }
    }
}
