using System;
using System.Drawing;
using System.Windows.Forms;

namespace HamStartup
{
    internal static class LinkProfileDialog
    {
        public static LinkProfile Show(IWin32Window owner, LinkProfile existing)
        {
            using (Form form = new Form())
            {
                form.Text = existing == null ? "添加连接方案" : "编辑连接方案";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(560, 450);
                form.BackColor = Color.White;
                form.Font = UiTheme.Font(9.5f);

                int y = 18;
                AddLabel(form, "方案名称", 18, y);
                TextBox name = AddText(form, 18, y + 20, 520);
                y += 58;

                AddLabel(form, "连接类型", 18, y);
                ComboBox protocol = new ComboBox();
                protocol.DropDownStyle = ComboBoxStyle.DropDownList;
                protocol.Items.AddRange(new object[] { "UDP", "TCP", "串口", "其他" });
                protocol.Location = new Point(18, y + 20);
                protocol.Width = 120;
                protocol.SelectedIndex = 0;
                form.Controls.Add(protocol);

                AddLabel(form, "匹配软件（名称关键字，可空）", 160, y);
                TextBox app = AddText(form, 160, y + 20, 378);
                y += 58;

                AddLabel(form, "本地地址 / 串口", 18, y);
                TextBox localAddr = AddText(form, 18, y + 20, 240);
                AddLabel(form, "本地端口", 278, y);
                NumericUpDown localPort = AddPort(form, 278, y + 20, 90);
                AddLabel(form, "远端地址", 388, y);
                TextBox remoteAddr = AddText(form, 388, y + 20, 150);
                y += 58;

                AddLabel(form, "远端端口", 18, y);
                NumericUpDown remotePort = AddPort(form, 18, y + 20, 90);
                AddLabel(form, "方向", 128, y);
                ComboBox direction = new ComboBox();
                direction.DropDownStyle = ComboBoxStyle.DropDownList;
                direction.Items.AddRange(new object[] { "发送", "监听", "连接", "串口", "其他" });
                direction.Location = new Point(128, y + 20);
                direction.Width = 110;
                direction.SelectedIndex = 0;
                form.Controls.Add(direction);

                CheckBox enabled = new CheckBox();
                enabled.Text = "启用此方案";
                enabled.AutoSize = true;
                enabled.Location = new Point(270, y + 24);
                enabled.Checked = true;
                form.Controls.Add(enabled);

                CheckBox autoMatch = new CheckBox();
                autoMatch.Text = "自动匹配";
                autoMatch.AutoSize = true;
                autoMatch.Location = new Point(400, y + 24);
                autoMatch.Checked = true;
                form.Controls.Add(autoMatch);
                y += 58;

                AddLabel(form, "备注", 18, y);
                TextBox notes = AddText(form, 18, y + 20, 520);
                y += 58;

                Label tip = UiTheme.MakeLabel("UDP 常用地址 239.255.0.1 / 127.0.0.1；TCP 集群常用端口 7300；串口填 COM10 等。", 8.4f, UiTheme.TextMuted);
                tip.Location = new Point(18, y);
                form.Controls.Add(tip);

                Button ok = UiTheme.PrimaryButton("保存", 100, 34);
                ok.Location = new Point(332, 400);
                ok.DialogResult = DialogResult.OK;
                form.Controls.Add(ok);

                Button cancel = UiTheme.SecondaryButton("取消", 90, 34);
                cancel.Location = new Point(442, 400);
                cancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(cancel);

                if (existing != null)
                {
                    name.Text = existing.Name;
                    protocol.SelectedItem = existing.Protocol;
                    if (protocol.SelectedIndex < 0) protocol.SelectedIndex = 0;
                    app.Text = existing.AppMatch;
                    localAddr.Text = existing.LocalAddress;
                    localPort.Value = Math.Max(0, Math.Min(65535, existing.LocalPort));
                    remoteAddr.Text = existing.RemoteAddress;
                    remotePort.Value = Math.Max(0, Math.Min(65535, existing.RemotePort));
                    direction.SelectedItem = existing.Direction;
                    if (direction.SelectedIndex < 0) direction.SelectedIndex = 0;
                    notes.Text = existing.Notes;
                    enabled.Checked = existing.Enabled;
                    autoMatch.Checked = existing.AutoMatch;
                }

                form.AcceptButton = ok;
                form.CancelButton = cancel;

                LinkProfile result = null;
                ok.Click += delegate
                {
                    if (name.Text.Trim().Length == 0)
                    {
                        MessageBox.Show(form, "请填写方案名称。", "连接方案",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    result = new LinkProfile();
                    result.Id = existing == null ? "link_" + DateTime.Now.Ticks : existing.Id;
                    result.Name = name.Text.Trim();
                    result.Protocol = protocol.SelectedItem.ToString();
                    result.AppMatch = app.Text.Trim();
                    result.LocalAddress = localAddr.Text.Trim();
                    result.LocalPort = (int)localPort.Value;
                    result.RemoteAddress = remoteAddr.Text.Trim();
                    result.RemotePort = (int)remotePort.Value;
                    result.Direction = direction.SelectedItem.ToString();
                    result.Notes = notes.Text.Trim();
                    result.Enabled = enabled.Checked;
                    result.AutoMatch = autoMatch.Checked;
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };

                if (form.ShowDialog(owner) != DialogResult.OK) return null;
                return result;
            }
        }

        private static void AddLabel(Form form, string text, int x, int y)
        {
            Label label = UiTheme.MakeLabel(text, 8.5f, UiTheme.TextMuted);
            label.Location = new Point(x, y);
            form.Controls.Add(label);
        }

        private static TextBox AddText(Form form, int x, int y, int width)
        {
            TextBox box = new TextBox();
            box.Location = new Point(x, y);
            box.Width = width;
            box.Font = UiTheme.Font(9f);
            form.Controls.Add(box);
            return box;
        }

        private static NumericUpDown AddPort(Form form, int x, int y, int width)
        {
            NumericUpDown box = new NumericUpDown();
            box.Location = new Point(x, y);
            box.Width = width;
            box.Minimum = 0;
            box.Maximum = 65535;
            box.Font = UiTheme.Font(9f);
            box.TextAlign = HorizontalAlignment.Right;
            form.Controls.Add(box);
            return box;
        }
    }
}
