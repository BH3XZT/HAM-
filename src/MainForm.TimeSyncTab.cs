using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace HamStartup
{
    public partial class MainForm
    {
        private TextBox _tsExePath;
        private ComboBox _tsTransport;
        private TextBox _tsPort;
        private NumericUpDown _tsWait;
        private CheckBox _tsAdmin;
        private CheckBox _tsAuto;
        private Label _tsStatus;
        private RichTextBox _tsOutput;
        private Button _tsBtnTest;
        private Button _tsBtnDry;
        private Button _tsBtnSync;
        private bool _tsBusy;
        private bool _tsLoading;

        private void BuildTimeSyncTab(Panel host)
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = UiTheme.Bg;

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Top;
            card.Height = 316;
            card.Padding = new Padding(0);

            Label title = UiTheme.MakeBoldLabel("授时 / IC-705 GPS 时间同步", 11.5f, UiTheme.Text);
            title.Location = new Point(18, 12);
            card.Controls.Add(title);

            Label hint = UiTheme.MakeLabel(
                "通过 ic705timesync 读取 IC-705 内置 GPS 校准电脑时钟。WiFi 直连暂不支持，请使用串口方式（RemoteUty 虚拟串口 / USB）。",
                8.8f, UiTheme.TextMuted);
            hint.AutoSize = false;
            hint.Width = 1060;
            hint.Height = 18;
            hint.Location = new Point(18, 40);
            card.Controls.Add(hint);

            Label exeLabel = UiTheme.MakeLabel("ic705timesync.exe 路径", 8.6f, UiTheme.TextMuted);
            exeLabel.Location = new Point(18, 68);
            card.Controls.Add(exeLabel);

            _tsExePath = new TextBox();
            _tsExePath.Location = new Point(18, 86);
            _tsExePath.Width = 780;
            _tsExePath.Font = UiTheme.Font(8.8f);
            _tsExePath.BorderStyle = BorderStyle.FixedSingle;
            _tsExePath.Leave += delegate { SaveTimeSyncSettings(); };
            card.Controls.Add(_tsExePath);

            Button browse = UiTheme.SmallButton("浏览…", 80, 28);
            browse.Location = new Point(806, 85);
            browse.Click += delegate { BrowseTimeSyncExe(); };
            card.Controls.Add(browse);

            Button detect = UiTheme.SmallButton("自动查找", 90, 28);
            detect.Location = new Point(894, 85);
            detect.Click += delegate { AutoFindTimeSyncExe(); };
            card.Controls.Add(detect);

            Label transportLabel = UiTheme.MakeLabel("连接方式", 8.6f, UiTheme.TextMuted);
            transportLabel.Location = new Point(18, 124);
            card.Controls.Add(transportLabel);

            _tsTransport = new ComboBox();
            _tsTransport.DropDownStyle = ComboBoxStyle.DropDownList;
            _tsTransport.Items.AddRange(new object[] { "串口（RemoteUty / USB）" });
            _tsTransport.Location = new Point(18, 142);
            _tsTransport.Width = 250;
            _tsTransport.Font = UiTheme.Font(8.8f);
            _tsTransport.SelectedIndexChanged += delegate { OnTimeSyncTransportChanged(); };
            card.Controls.Add(_tsTransport);

            Label portLabel = UiTheme.MakeLabel("串口（auto=自动识别）", 8.6f, UiTheme.TextMuted);
            portLabel.Location = new Point(296, 124);
            card.Controls.Add(portLabel);

            _tsPort = new TextBox();
            _tsPort.Location = new Point(296, 142);
            _tsPort.Width = 140;
            _tsPort.Font = UiTheme.Font(8.8f);
            _tsPort.BorderStyle = BorderStyle.FixedSingle;
            _tsPort.Leave += delegate { SaveTimeSyncSettings(); };
            card.Controls.Add(_tsPort);

            // WiFi 直连已移除（独立 exe 无法加载 icom-lan）。这里保留一条禁用的说明，
            // 避免旧截图/旧说明里的“电台 IP”输入框继续误导用户。
            Label lanNote = UiTheme.MakeLabel("WiFi 直连（已移除）", 8.6f, UiTheme.TextFaint);
            lanNote.Location = new Point(456, 124);
            card.Controls.Add(lanNote);

            Label lanNote2 = UiTheme.MakeLabel("请使用串口方式", 8.6f, UiTheme.TextFaint);
            lanNote2.Location = new Point(456, 146);
            card.Controls.Add(lanNote2);

            Label waitLabel = UiTheme.MakeLabel("等待秒数", 8.6f, UiTheme.TextMuted);
            waitLabel.Location = new Point(646, 124);
            card.Controls.Add(waitLabel);

            _tsWait = new NumericUpDown();
            _tsWait.Location = new Point(646, 142);
            _tsWait.Width = 70;
            _tsWait.Minimum = 5;
            _tsWait.Maximum = 300;
            _tsWait.Font = UiTheme.Font(8.8f);
            _tsWait.BorderStyle = BorderStyle.FixedSingle;
            _tsWait.ValueChanged += delegate { SaveTimeSyncSettings(); };
            card.Controls.Add(_tsWait);

            _tsAdmin = new CheckBox();
            _tsAdmin.Text = "以管理员身份对时（会弹 UAC）";
            _tsAdmin.Location = new Point(18, 186);
            _tsAdmin.AutoSize = true;
            _tsAdmin.Font = UiTheme.Font(9f);
            _tsAdmin.BackColor = Color.White;
            _tsAdmin.CheckedChanged += delegate { SaveTimeSyncSettings(); };
            card.Controls.Add(_tsAdmin);

            _tsAuto = new CheckBox();
            _tsAuto.Text = "启动流程开始前自动授时";
            _tsAuto.Location = new Point(300, 186);
            _tsAuto.AutoSize = true;
            _tsAuto.Font = UiTheme.Font(9f);
            _tsAuto.BackColor = Color.White;
            _tsAuto.CheckedChanged += delegate { SaveTimeSyncSettings(); };
            card.Controls.Add(_tsAuto);

            _tsBtnTest = UiTheme.PrimaryButton("检测连接", 130, 36);
            _tsBtnTest.Location = new Point(18, 220);
            _tsBtnTest.Click += delegate { RunTimeSync("read"); };
            card.Controls.Add(_tsBtnTest);

            _tsBtnDry = UiTheme.SecondaryButton("试对时（不改时间）", 176, 36);
            _tsBtnDry.Location = new Point(158, 220);
            _tsBtnDry.Click += delegate { RunTimeSync("dryrun"); };
            card.Controls.Add(_tsBtnDry);

            _tsBtnSync = UiTheme.PrimaryButton("立即对时", 120, 36);
            _tsBtnSync.Location = new Point(344, 220);
            _tsBtnSync.Click += delegate { RunTimeSync("sync"); };
            card.Controls.Add(_tsBtnSync);

            _tsStatus = UiTheme.MakeLabel("未检测", 9f, UiTheme.TextMuted);
            _tsStatus.AutoSize = false;
            _tsStatus.Width = 990;
            _tsStatus.Height = 18;
            _tsStatus.Location = new Point(18, 268);
            card.Controls.Add(_tsStatus);

            Panel outputPanel = new Panel();
            outputPanel.Dock = DockStyle.Fill;
            outputPanel.Padding = new Padding(0, 8, 0, 0);
            page.Controls.Add(outputPanel);
            page.Controls.Add(card);   // Fill 先加、Top 后加，避免 Fill 盖住设置卡

            CardPanel outputCard = new CardPanel();
            outputCard.Dock = DockStyle.Fill;
            outputPanel.Controls.Add(outputCard);

            Label outputCaption = UiTheme.MakeBoldLabel("输出", 9.5f, UiTheme.Text);
            outputCaption.Location = new Point(18, 10);
            outputCard.Controls.Add(outputCaption);

            _tsOutput = new RichTextBox();
            _tsOutput.Dock = DockStyle.Fill;
            _tsOutput.ReadOnly = true;
            _tsOutput.BorderStyle = BorderStyle.None;
            _tsOutput.Font = UiTheme.Font(9f);
            _tsOutput.BackColor = Color.White;
            _tsOutput.ForeColor = UiTheme.Text;
            _tsOutput.WordWrap = true;
            _tsOutput.ScrollBars = RichTextBoxScrollBars.Vertical;
            _tsOutput.Location = new Point(18, 34);
            outputCard.Controls.Add(_tsOutput);
            try { outputCard.Controls.SetChildIndex(_tsOutput, 0); } catch { }
            outputCard.Padding = new Padding(18, 38, 18, 14);

            host.Controls.Add(page);
            LoadTimeSyncSettings();
        }

        private void LoadTimeSyncSettings()
        {
            if (_settings == null) return;
            TimeSyncSettings ts = _settings.TimeSync;
            if (ts == null) return;

            _tsLoading = true;
            try
            {
                string exe = TimeSyncRunner.ResolveExe(ts);
                _tsExePath.Text = string.IsNullOrEmpty(exe) ? (ts.ExePath ?? "") : exe;
                _tsTransport.SelectedIndex = 0;
                _tsPort.Text = string.IsNullOrEmpty(ts.Port) ? "auto" : ts.Port;
                _tsWait.Value = Math.Max((int)_tsWait.Minimum, Math.Min((int)_tsWait.Maximum, ts.WaitSeconds));
                _tsAdmin.Checked = ts.RunAsAdmin;
                _tsAuto.Checked = ts.AutoBeforeStart;
                _tsStatus.Text = string.IsNullOrEmpty(ts.LastResult) ? "未检测" : ts.LastResult;
                _tsStatus.ForeColor = UiTheme.TextMuted;
                UpdateTimeSyncEnabledState();
            }
            finally
            {
                _tsLoading = false;
            }
        }

        private void SaveTimeSyncSettings()
        {
            if (_tsLoading) return;
            if (_settings == null || _settings.TimeSync == null) return;
            TimeSyncSettings ts = _settings.TimeSync;
            // WiFi 直连已移除：配置里固定写 serial，旧的 host 值保持原样以便回退旧版本。
            ts.Transport = "serial";
            ts.ExePath = (_tsExePath.Text ?? "").Trim();
            ts.Port = string.IsNullOrEmpty((_tsPort.Text ?? "").Trim()) ? "auto" : _tsPort.Text.Trim();
            ts.WaitSeconds = (int)_tsWait.Value;
            ts.RunAsAdmin = _tsAdmin.Checked;
            ts.AutoBeforeStart = _tsAuto.Checked;
            UpdateTimeSyncEnabledState();
            TrySave();
        }

        private void UpdateTimeSyncEnabledState()
        {
            // 只剩串口一种方式，串口输入框始终可用（忙碌时由 SetTimeSyncBusy 统一禁用）。
            if (_tsPort != null) _tsPort.Enabled = !_tsBusy;
        }

        private void OnTimeSyncTransportChanged()
        {
            if (_tsLoading) return;
            UpdateTimeSyncEnabledState();
            SaveTimeSyncSettings();
        }

        private void SetTimeSyncBusy(bool busy)
        {
            if (_tsBtnTest != null) _tsBtnTest.Enabled = !busy;
            if (_tsBtnDry != null) _tsBtnDry.Enabled = !busy;
            if (_tsBtnSync != null) _tsBtnSync.Enabled = !busy;
            if (_tsTransport != null) _tsTransport.Enabled = !busy;
            if (_tsPort != null) _tsPort.Enabled = !busy;
            if (_tsWait != null) _tsWait.Enabled = !busy;
            if (_tsAdmin != null) _tsAdmin.Enabled = !busy;
            if (_tsAuto != null) _tsAuto.Enabled = !busy;
        }

        private void SetTimeSyncStatus(string text, Color color)
        {
            if (_tsStatus == null) return;
            _tsStatus.Text = text ?? "";
            _tsStatus.ForeColor = color;
        }

        /// <summary>输出框与状态栏保持同一份内容：所有失败路径都必须走这里，避免“状态栏有原因、输出框空白”。</summary>
        private void SetTimeSyncOutput(string text)
        {
            if (_tsOutput == null) return;
            _tsOutput.Text = string.IsNullOrEmpty(text) ? "（无输出）" : text;
        }

        private static string JoinResultText(TimeSyncResult result)
        {
            StringBuilder text = new StringBuilder();
            if (!string.IsNullOrEmpty(result.Output)) text.Append(result.Output.TrimEnd());
            if (!string.IsNullOrEmpty(result.Error))
            {
                if (text.Length > 0) text.AppendLine();
                text.Append("[错误] ").Append(result.Error.Trim());
            }
            if (text.Length == 0) text.Append("（无输出，退出码 ").Append(result.ExitCode).Append("）");
            return text.ToString();
        }

        private void BrowseTimeSyncExe()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 ic705timesync.exe";
                dialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                string current = (_tsExePath.Text ?? "").Trim();
                if (current.Length > 0)
                {
                    try { dialog.InitialDirectory = Path.GetDirectoryName(current); }
                    catch { }
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _tsExePath.Text = dialog.FileName;
                    SaveTimeSyncSettings();
                }
            }
        }

        private void AutoFindTimeSyncExe()
        {
            string found = TimeSyncRunner.ResolveExe(_settings.TimeSync);
            if (string.IsNullOrEmpty(found))
            {
                SetTimeSyncStatus("没有找到 ic705timesync.exe，请点“浏览…”手动指定", UiTheme.Red);
                SetTimeSyncOutput("没有找到 ic705timesync.exe。\n请点“浏览…”手动指定程序路径，\n或把 ic705timesync.exe 放到 HamStartup.exe 同一目录。");
                AppendLog("  自动查找授时程序失败：没有找到 ic705timesync.exe", LogKind.Warn);
                return;
            }
            _tsExePath.Text = found;
            SaveTimeSyncSettings();
            SetTimeSyncStatus("已找到：" + found, UiTheme.TextMuted);
            SetTimeSyncOutput("已找到 ic705timesync.exe：\n" + found);
        }

        private static string FirstLine(string error, string output, int exitCode)
        {
            foreach (string source in new string[] { error, output })
            {
                if (string.IsNullOrEmpty(source)) continue;
                foreach (string line in source.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) return trimmed;
                }
            }
            return "退出码 " + exitCode;
        }

        private async void RunTimeSync(string action)
        {
            if (_tsBusy) return;
            SaveTimeSyncSettings();

            string exe = TimeSyncRunner.ResolveExe(_settings.TimeSync);
            if (string.IsNullOrEmpty(exe))
            {
                SetTimeSyncStatus("找不到 ic705timesync.exe", UiTheme.Red);
                SetTimeSyncOutput("找不到 ic705timesync.exe。\n请点“浏览…”指定程序路径，\n或把 ic705timesync.exe 放到 HamStartup.exe 同一目录。");
                AppendLog("  授时失败：找不到 ic705timesync.exe", LogKind.Warn);
                return;
            }

            _tsBusy = true;
            SetTimeSyncBusy(true);
            string label = action == "read" ? "检测连接" : (action == "dryrun" ? "试对时" : "对时");
            SetTimeSyncStatus(label + "中…", UiTheme.TextMuted);
            if (_tsOutput != null) _tsOutput.Text = "";

            try
            {
                if (action == "sync")
                {
                    // 先做一次只读检查，确认链路可通；失败时能给出可读原因，也避免无谓的 UAC。
                    TimeSyncResult probe = await TimeSyncRunner.RunAsync(_settings.TimeSync, "read");
                    if (!probe.Success)
                    {
                        string reason = FirstLine(probe.Error, probe.Output, probe.ExitCode);
                        SetTimeSyncStatus("对时前检查失败：" + reason, UiTheme.Red);
                        SetTimeSyncOutput("对时前检查失败：" + reason + Environment.NewLine +
                                          "（只读检查的退出码 " + probe.ExitCode + "，未改动系统时间）" + Environment.NewLine +
                                          JoinResultText(probe));
                        AppendLog("  对时前检查失败：" + reason, LogKind.Warn);
                        AppendLog("  建议：1) 确认 RemoteUty 已连上电台（USB 线或虚拟串口）；2) 关闭 JTDX 后再试（虚拟串口独占）；3) 串口填 auto 或具体 COM 号。", LogKind.Info);
                        _settings.TimeSync.LastResult = _tsStatus.Text;
                        try { TrySave(); } catch { }
                        return;
                    }
                }

                AppendLog("▶ 授时：" + label + "（" + exe + "）", LogKind.Step);
                AppendLog("  参数：" + TimeSyncRunner.BuildArguments(_settings.TimeSync, action), LogKind.Info);

                TimeSyncResult result = await TimeSyncRunner.RunAsync(_settings.TimeSync, action);
                string detail = JoinResultText(result);

                if (result.Success)
                {
                    SetTimeSyncStatus(string.Format("{0}成功（{1:0.0} 秒）", label, result.Seconds), UiTheme.Green);
                    SetTimeSyncOutput(detail);
                    AppendLog("  " + label + "成功，用时 " + result.Seconds.ToString("0.0") + " 秒", LogKind.Success);
                }
                else
                {
                    string reason = string.IsNullOrEmpty(result.Error) ? ("退出码 " + result.ExitCode) : result.Error.Trim();
                    if (action == "sync" && _settings.TimeSync.RunAsAdmin)
                        reason += "（管理员模式下无法捕获输出，可先点“检测连接”查看原因）";
                    SetTimeSyncStatus(label + "失败：" + reason, UiTheme.Red);
                    SetTimeSyncOutput(label + "失败：" + reason + Environment.NewLine + detail);
                    AppendLog("  " + label + "失败：" + reason, LogKind.Warn);
                }
            }
            catch (Exception ex)
            {
                SetTimeSyncStatus(label + "失败：" + ex.Message, UiTheme.Red);
                SetTimeSyncOutput(label + "失败：" + ex.Message);
                AppendLog("  " + label + "失败：" + ex.Message, LogKind.Warn);
            }
            finally
            {
                _tsBusy = false;
                SetTimeSyncBusy(false);
                UpdateTimeSyncEnabledState();
            }

            _settings.TimeSync.LastResult = _tsStatus.Text;
            try { TrySave(); } catch { }
        }
    }
}
