using System;
using System.Drawing;
using System.Windows.Forms;

namespace HamStartup
{
    internal static class InputDialog
    {
        public static string Show(IWin32Window owner, string title, string label, string value)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(420, 150);
                form.BackColor = Color.White;
                form.Font = UiTheme.Font(9.5f);

                Label caption = new Label();
                caption.Text = label;
                caption.AutoSize = true;
                caption.Location = new Point(20, 20);
                caption.ForeColor = UiTheme.Text;
                form.Controls.Add(caption);

                TextBox box = new TextBox();
                box.Text = value ?? "";
                box.Location = new Point(20, 46);
                box.Width = 380;
                box.Font = UiTheme.Font(10f);
                form.Controls.Add(box);

                Button ok = UiTheme.PrimaryButton("确定", 90, 32);
                ok.Location = new Point(210, 100);
                ok.DialogResult = DialogResult.OK;
                form.Controls.Add(ok);

                Button cancel = UiTheme.SecondaryButton("取消", 90, 32);
                cancel.Location = new Point(310, 100);
                cancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(cancel);

                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(owner) != DialogResult.OK) return null;
                return box.Text.Trim();
            }
        }
    }
}
