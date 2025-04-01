using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmartMines
{
    public static class ActionDialogHelper
    {
        public static void ShowStateSelectionDialog(string title, Action<LightState> onSelect)
        {
            Form actionForm = new Form();
            actionForm.Text = title;
            actionForm.Size = new Size(300, 300);
            actionForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            actionForm.StartPosition = FormStartPosition.CenterParent;

            string[] labels = { "On (Green)", "Off (Gray)", "Danger (Red)", "Warning (Orange)", "Processing (Blue)" };
            LightState[] states = { LightState.On, LightState.Off, LightState.Danger, LightState.Warning, LightState.Processing };

            for (int i = 0; i < labels.Length; i++)
            {
                Button btn = new Button();
                btn.Text = labels[i];
                btn.Tag = states[i];
                btn.Size = new Size(200, 30);
                btn.Location = new Point(40, 30 + i * 40);
                btn.Click += (s, e) =>
                {
                    var selected = (LightState)((Button)s).Tag;
                    onSelect?.Invoke(selected);
                    actionForm.Close();
                };
                actionForm.Controls.Add(btn);
            }

            actionForm.ShowDialog();
        }
    }
}
