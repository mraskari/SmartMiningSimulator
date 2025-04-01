using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmartMines
{
    public enum LightState { Off, On, Danger, Warning, Processing }

    public class CircularLight : Button
    {
        public string MiningSiteId { get; private set; }
        public int SectionId { get; private set; }
        public int LightId { get; private set; }

        public Color LightColor { get; private set; }
        public LightState State { get; private set; }

        public bool IsOn
        {
            get { return State == LightState.On; }
            set { if (value) SetState(LightState.On); else SetState(LightState.Off); }
        }
        public bool IsDanger
        {
            get { return State == LightState.Danger; }
            set { if (value) SetState(LightState.Danger); }
        }
        public bool IsWarning
        {
            get { return State == LightState.Warning; }
            set { if (value) SetState(LightState.Warning); }
        }
        public bool IsProcessing
        {
            get { return State == LightState.Processing; }
            set { if (value) SetState(LightState.Processing); }
        }

        public static event Action OnAnyLightChanged;

        private bool isHovered = false;
        private string ToolTipText;

        public CircularLight(string siteId, int sectionId, int lightId)
        {
            ToolTipText = $"Site: {siteId}\nSection: {sectionId}\nLight: {lightId}\nState: Off";
            MiningSiteId = siteId;
            SectionId = sectionId;
            LightId = lightId;
            State = LightState.Off;
            LightColor = Color.LightGray;

            FlatStyle = FlatStyle.Flat;
            BackColor = Color.Transparent;
            TabStop = false;
            FlatAppearance.BorderSize = 0;
            this.MouseHover += CircularLight_MouseHover;
            this.Cursor = Cursors.Hand;
        }

        private void CircularLight_MouseHover(object sender, EventArgs e)
        {
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(this, ToolTipText);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(this.Parent.BackColor);

            using (SolidBrush brush = new SolidBrush(LightColor))
            {
                g.FillEllipse(brush, 0, 0, this.Width - 1, this.Height - 1);
            }

            Color borderColor = isHovered ? Color.Black : Color.Gray;
            using (Pen pen = new Pen(borderColor, 2))
            {
                g.DrawEllipse(pen, 1, 1, this.Width - 3, this.Height - 3);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ActionDialogHelper.ShowStateSelectionDialog($"Select Action for Light {SectionId}-{LightId}", selectedState =>
            {
                SetState(selectedState);
                MqttHandler.PublishLightStatus(MiningSiteId, SectionId, LightId, State.ToString().ToLower());
                OnAnyLightChanged?.Invoke();
            });
        }

        public void ToggleStateOnly()
        {
            if (State == LightState.On)
            {
                SetState(LightState.Off);
            }
            else
            {
                SetState(LightState.On);
            }
            OnAnyLightChanged?.Invoke();
        }

        public void ChangeColor(Color newColor)
        {
            LightColor = newColor;
            Invalidate();
        }

        public void SetState(LightState newState)
        {
            ToolTipText = $"Site: {MiningSiteId}\nSection: {SectionId}\nLight: {LightId}\nState: {newState}";
            State = newState;
            switch (newState)
            {
                case LightState.On: LightColor = Color.Green; break;
                case LightState.Danger: LightColor = Color.Red; break;
                case LightState.Warning: LightColor = Color.Orange; break;
                case LightState.Processing: LightColor = Color.Blue; break;
                case LightState.Off:
                default: LightColor = Color.LightGray; break;
            }
            Invalidate();
        }
    }
}
