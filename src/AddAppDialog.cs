using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HamStartup
{
    public class AddAppResult
    {
        public string Name = "";
        public string Path = "";
    }

    internal static class AddAppDialog
    {
        public static AddAppResult Show(IWin32Window owner)
        {
            using (Form form = new Form())
            {
                form.Text = "添加 Ham 软件";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(640, 470);
                form.BackColor = Color.White;
                form.Font = UiTheme.Font(9.5f);

                Label title = UiTheme.MakeBoldLabel("已检测到的 Ham 软件", 11f, UiTheme.Text);
                title.Location = new Point(18, 12);
                form.Controls.Add(title);

                Label hint = UiTheme.MakeLabel("双击列表项即可填入；也可以点“浏览”手动选择 exe 或桌面快捷方式。", 8.5f, UiTheme.TextMuted);
                hint.Location = new Point(18, 36);
                form.Controls.Add(hint);

                ListBox list = new ListBox();
                list.Location = new Point(18, 60);
                list.Size = new Size(604, 200);
                list.Font = UiTheme.Font(9f);
                list.IntegralHeight = false;
                form.Controls.Add(list);

                List<InstalledAppCandidate> candidates = null;
                try
                {
                    candidates = InstalledAppScanner.Scan();
                }
                catch { candidates = new List<InstalledAppCandidate>(); }
                foreach (InstalledAppCandidate candidate in candidates) list.Items.Add(candidate);

                Label nameLabel = UiTheme.MakeLabel("软件名称", 8.8f, UiTheme.TextMuted);
                nameLabel.Location = new Point(18, 280);
                form.Controls.Add(nameLabel);

                TextBox nameBox = new TextBox();
                nameBox.Location = new Point(18, 300);
                nameBox.Width = 604;
                nameBox.Font = UiTheme.Font(9.5f);
                form.Controls.Add(nameBox);

                Label pathLabel = UiTheme.MakeLabel("程序 / 快捷方式路径", 8.8f, UiTheme.TextMuted);
                pathLabel.Location = new Point(18, 336);
                form.Controls.Add(pathLabel);

                TextBox pathBox = new TextBox();
                pathBox.Location = new Point(18, 356);
                pathBox.Width = 500;
                pathBox.Font = UiTheme.Font(9f);
                form.Controls.Add(pathBox);

                Button browse = UiTheme.SecondaryButton("浏览…", 90, 28);
                browse.Location = new Point(528, 355);
                browse.Click += delegate
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Title = "选择 Ham 软件或桌面快捷方式";
                        dialog.Filter = "程序或快捷方式 (*.exe;*.lnk)|*.exe;*.lnk|程序 (*.exe)|*.exe|快捷方式 (*.lnk)|*.lnk|所有文件 (*.*)|*.*";
                        if (dialog.ShowDialog(form) != DialogResult.OK) return;
                        pathBox.Text = dialog.FileName;
                        if (string.IsNullOrEmpty(nameBox.Text))
                        {
                            string name = Path.GetFileNameWithoutExtension(dialog.FileName);
                            try
                            {
                                if (dialog.FileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                                {
                                    ShortcutInfo info = ShortcutResolver.Resolve(dialog.FileName);
                                    if (info != null && info.Valid)
                                        name = Path.GetFileNameWithoutExtension(info.TargetPath);
                                }
                            }
                            catch { }
                            nameBox.Text = name;
                        }
                    }
                };
                form.Controls.Add(browse);

                list.SelectedIndexChanged += delegate
                {
                    InstalledAppCandidate selected = list.SelectedItem as InstalledAppCandidate;
                    if (selected == null) return;
                    nameBox.Text = selected.Name;
                    pathBox.Text = selected.Path;
                };
                list.DoubleClick += delegate
                {
                    InstalledAppCandidate selected = list.SelectedItem as InstalledAppCandidate;
                    if (selected == null) return;
                    nameBox.Text = selected.Name;
                    pathBox.Text = selected.Path;
                };

                Button ok = UiTheme.PrimaryButton("添加到方案", 110, 34);
                ok.Location = new Point(402, 414);
                form.Controls.Add(ok);

                Button cancel = UiTheme.SecondaryButton("取消", 90, 34);
                cancel.Location = new Point(522, 414);
                cancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(cancel);

                AddAppResult result = null;
                ok.Click += delegate
                {
                    string name = nameBox.Text.Trim();
                    string path = pathBox.Text.Trim().Trim('"');
                    if (name.Length == 0 || path.Length == 0)
                    {
                        MessageBox.Show(form, "请填写软件名称和路径。", "添加软件",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!File.Exists(path))
                    {
                        MessageBox.Show(form, "路径不存在：\n" + path, "添加软件",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".exe" && ext != ".lnk")
                    {
                        MessageBox.Show(form, "请选择 .exe 或 .lnk 文件。", "添加软件",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    result = new AddAppResult();
                    result.Name = name;
                    result.Path = path;
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };

                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(owner) != DialogResult.OK) return null;
                return result;
            }
        }
    }
}
