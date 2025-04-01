using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SmartMines
{
    public partial class MainForm : Form
    {
        private Panel[] sectionPanels = new Panel[4];
        private string miningSiteId = "";
        private TextBox siteIdTextBox;
        private Button siteSubmitButton;
        private Label sitePromptLabel;
        private Label connectingLabel;
        private Panel inputPanel;

        public MainForm()
        {
            InitializeComponent();
            this.Size = new Size(1200, 700);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            CircularLight.OnAnyLightChanged += new Action(UpdateLightStatsInTitle);

            CreateSectionInputs();
            CreateSiteIdPrompt();
        }

        private void ClearExistingLights(Panel panel)
        {
            for (int i = panel.Controls.Count - 1; i >= 0; i--)
            {
                if (panel.Controls[i] is CircularLight)
                    panel.Controls.RemoveAt(i);
            }
        }

        private string FormatLightStatus(int sectionId, int lightId, string state)
        {
            return $"section{sectionId}/light{lightId}={state}";
        }

        private List<string> CollectStatusesFromAllSections()
        {
            List<string> allStatuses = new List<string>();
            for (int s = 0; s < 4; s++)
            {
                foreach (Control ctrl in sectionPanels[s].Controls)
                {
                    CircularLight light = ctrl as CircularLight;
                    if (light != null)
                    {
                        allStatuses.Add(FormatLightStatus(light.SectionId, light.LightId, light.State.ToString().ToLower()));
                    }
                }
            }
            return allStatuses;
        }

        private void CreateSiteIdPrompt()
        {
            inputPanel = new Panel();
            inputPanel.Size = new Size(400, 180);
            inputPanel.Location = new Point((this.Width - 400) / 2, 10);

            sitePromptLabel = new Label();
            sitePromptLabel.Text = "Enter Mining Site ID:";
            sitePromptLabel.AutoSize = true;
            sitePromptLabel.Location = new Point(10, 20);

            siteIdTextBox = new TextBox();
            siteIdTextBox.Width = 250;
            siteIdTextBox.Location = new Point(10, 50);

            siteSubmitButton = new Button();
            siteSubmitButton.Text = "Submit";
            siteSubmitButton.Width = 80;
            siteSubmitButton.Location = new Point(270, 48);
            siteSubmitButton.Click += new EventHandler(SiteSubmitButton_Click);

            connectingLabel = new Label();
            connectingLabel.Location = new Point(10, 90);
            connectingLabel.AutoSize = true;
            connectingLabel.Text = "";

            inputPanel.Controls.Add(sitePromptLabel);
            inputPanel.Controls.Add(siteIdTextBox);
            inputPanel.Controls.Add(siteSubmitButton);
            inputPanel.Controls.Add(connectingLabel);
            this.Controls.Add(inputPanel);

            for (int i = 0; i < sectionPanels.Length; i++)
            {
                if (sectionPanels[i] != null)
                    sectionPanels[i].Visible = false;
            }
        }

        private async void SiteSubmitButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(siteIdTextBox.Text))
            {
                miningSiteId = siteIdTextBox.Text.Trim();
                siteIdTextBox.Enabled = false;
                siteSubmitButton.Enabled = false;
                connectingLabel.Text = "Connecting to MQTT broker...";

                try
                {
                    await MqttHandler.ConnectAsync(OnExternalMqttMessage);
                    inputPanel.Visible = false;
                    connectingLabel.Text = "";

                    for (int i = 0; i < sectionPanels.Length; i++)
                    {
                        if (sectionPanels[i] != null)
                            sectionPanels[i].Visible = true;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        if (sectionPanels[i] != null)
                        {
                            Control box = sectionPanels[i].Controls[2];
                            TextBox textBox = box as TextBox;
                            if (textBox != null)
                                GenerateLights(i + 1, textBox, sectionPanels[i], false);
                        }
                    }

                    List<string> allStatuses = GetAllLightStatuses();
                    MqttHandler.PublishAllSectionsStatus(miningSiteId, allStatuses);

                    UpdateLightStatsInTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to connect to MQTT: " + ex.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    siteIdTextBox.Enabled = true;
                    siteSubmitButton.Enabled = true;
                    connectingLabel.Text = "";
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid Site ID.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                siteIdTextBox.Enabled = true;
                siteSubmitButton.Enabled = true;
            }
        }

        private List<string> GetAllLightStatuses()
        {
            return CollectStatusesFromAllSections();
        }

        private void OnExternalMqttMessage(string topic, string payload)
        {
            this.Invoke((MethodInvoker)delegate
            {
                try
                {
                    IncomingPayload data = JsonConvert.DeserializeObject<IncomingPayload>(payload);
                    if (data != null && (data.siteId != miningSiteId || data.sender == MqttHandler.clientId)) return;

                    var parts = topic.Split('/');
                    if (parts.Length == 6 && parts[0] == "sites" && parts[2] == "sections" && parts[4] == "lights")
                    {
                        string siteId = parts[1];
                        int sectionId = int.Parse(parts[3]);
                        int lightId = int.Parse(parts[5]);

                        LightState newState;
                        if (Enum.TryParse<LightState>(data.state, true, out newState))
                        {
                            foreach (var panel in sectionPanels)
                            {
                                foreach (Control ctrl in panel.Controls)
                                {
                                    CircularLight light = ctrl as CircularLight;
                                    if (light != null && light.SectionId == sectionId && light.LightId == lightId)
                                    {
                                        light.SetState(newState);
                                        UpdateLightStatsInTitle();

                                        MqttHandler.PublishLightStatus(
                                            miningSiteId,
                                            sectionId,
                                            lightId,
                                            newState.ToString().ToLower()
                                        );

                                        return;
                                    }
                                }
                            }
                        }
                    }
                    else if (parts.Length == 5 && parts[0] == "sites" && parts[2] == "sections" && parts[4] == "lights")
                    {
                        // section update: sites/{siteId}/sections/{sectionId}/lights
                        int sectionId = int.Parse(parts[3]);
                        if (data.lights != null)
                        {
                            foreach (var entry in data.lights)
                            {
                                int lid = (int)entry.id;
                                string st = (string)entry.state;

                                foreach (var panel in sectionPanels)
                                {
                                    foreach (Control ctrl in panel.Controls)
                                    {
                                        CircularLight light = ctrl as CircularLight;
                                        if (light != null && light.SectionId == sectionId && light.LightId == lid)
                                        {
                                            LightState newState;
                if (Enum.TryParse<LightState>(st, true, out newState))
                {
                                                light.SetState(newState);
                }
            }
        }
    }
}

// re-publish so web sees final states
List<string> newStatuses = new List<string>();
                            foreach (Control ctrl in sectionPanels[sectionId - 1].Controls)
                            {
                                CircularLight cLight = ctrl as CircularLight;
                                if (cLight != null)
                                {
                                    newStatuses.Add(FormatLightStatus(
                                        cLight.SectionId,
                                        cLight.LightId,
                                        cLight.State.ToString().ToLower()
                                    ));
                                }
                            }
                            MqttHandler.PublishSectionStatus(miningSiteId, sectionId, newStatuses);
                            UpdateLightStatsInTitle();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error parsing incoming MQTT command: " + ex.Message);
                }
            });
        }

        public class IncomingPayload
        {
            public string siteId { get; set; }
            public string sender { get; set; }
            public string state { get; set; }
            public int id { get; set; }
            public int section { get; set; }
            public List<LightData> lights { get; set; }
        }

        public class LightData
        {
            public int id { get; set; }
            public string state { get; set; }
        }

        private void CreateSectionInputs()
        {
            for (int i = 0; i < 4; i++)
            {

                Panel sectionPanel = new Panel();
                sectionPanel.Size = new Size(260, 610);
                sectionPanel.Location = new Point(30 + i * 290, 25);
                sectionPanel.BorderStyle = BorderStyle.None;
                sectionPanel.BackColor = Color.WhiteSmoke;
                sectionPanel.Padding = new Padding(5);
                sectionPanel.AutoScroll = true;
                sectionPanel.Paint += (s, e) =>
                {
                    ControlPaint.DrawBorder(e.Graphics, sectionPanel.ClientRectangle,
                        Color.White, 2, ButtonBorderStyle.Solid,
                        Color.White, 2, ButtonBorderStyle.Solid,
                        Color.White, 2, ButtonBorderStyle.Solid,
                        Color.White, 2, ButtonBorderStyle.Solid);
                };

                Label sectionLabel = new Label();
                sectionLabel.Text = $" Section {i + 1}";
                sectionLabel.Size = new Size(260, 16);
                sectionLabel.Location = new Point(0, 0);
                sectionLabel.TextAlign = ContentAlignment.MiddleLeft;
                sectionLabel.BackColor = Color.White;

                Label countLabel = new Label();
                countLabel.Text = "Lights:";
                countLabel.Location = new Point(8, 30);
                countLabel.Size = new Size(40, 25);

                TextBox textBox = new TextBox();
                textBox.Size = new Size(37, 25);
                textBox.Location = new Point(47, 28);
                textBox.Text = "126";

                Button generateButton = new Button();
                generateButton.Text = "Generate";
                generateButton.Size = new Size(65, 25);
                generateButton.Location = new Point(95, 26);

                Button actionsButton = new Button();
                actionsButton.Text = "Actions";
                actionsButton.Size = new Size(65, 25);
                actionsButton.Location = new Point(165, 26);

                int sectionIndex = i + 1;
                generateButton.Click += delegate { GenerateLights(sectionIndex, textBox, sectionPanel, true); };
                actionsButton.Click += delegate {
                    ActionDialogHelper.ShowStateSelectionDialog($"Select Action for Section {sectionIndex}", selectedState =>
                    {
                        List<string> sectionStatus = new List<string>();

                        foreach (Control ctrl in sectionPanels[sectionIndex - 1].Controls)
                        {
                            CircularLight light = ctrl as CircularLight;
                            if (light != null)
                            {
                                light.SetState(selectedState);
                                sectionStatus.Add($"{FormatLightStatus(sectionIndex, light.LightId, selectedState.ToString().ToLower())}");
                            }
                        }

                        MqttHandler.PublishSectionStatus(miningSiteId, sectionIndex, sectionStatus);
                        UpdateLightStatsInTitle();
                    });
                };

                sectionPanel.Controls.Add(sectionLabel);
                sectionPanel.Controls.Add(countLabel);
                sectionPanel.Controls.Add(textBox);
                sectionPanel.Controls.Add(generateButton);
                sectionPanel.Controls.Add(actionsButton);

                sectionPanels[i] = sectionPanel;
                this.Controls.Add(sectionPanel);
            }
        }

        private void GenerateLights(int sectionId, TextBox inputBox, Panel panel, bool sendMessage)
        {
            int count;
            if (!int.TryParse(inputBox.Text.Trim(), out count) || count < 1)
            {
                return;
            }

            ClearExistingLights(panel);

            List<string> sectionStatus = new List<string>();

            for (int i = 0; i < count; i++)
            {
                CircularLight light = new CircularLight(miningSiteId, sectionId, i + 1);
                light.Size = new Size(24, 24);
                light.Location = new Point(10 + (i % 7) * 30, 62 + (i / 7) * 30);
                panel.Controls.Add(light);
                sectionStatus.Add(FormatLightStatus(sectionId, i + 1, "off"));
            }

            if (sendMessage)
            {
                MqttHandler.PublishSectionStatus(miningSiteId, sectionId, sectionStatus);
            }

            UpdateLightStatsInTitle();
        }
        
        private void UpdateLightStatsInTitle()
        {
            int total = 0, on = 0, off = 0, red = 0, orange = 0, blue = 0;

            for (int i = 0; i < sectionPanels.Length; i++)
            {
                Panel panel = sectionPanels[i];
                if (panel == null) continue;

                foreach (Control ctrl in panel.Controls)
                {
                    CircularLight light = ctrl as CircularLight;
                    if (light != null)
                    {
                        total++;
                        switch (light.State)
                        {
                            case LightState.On: on++; break;
                            case LightState.Danger: red++; break;
                            case LightState.Warning: orange++; break;
                            case LightState.Processing: blue++; break;
                            case LightState.Off:
                            default: off++; break;
                        }
                    }
                }
            }

            this.Text = string.Format(
                "MQTT Panel - Site: {0} | Total: {1}, On: {2}, Off: {3}, Danger: {4}, Warning: {5}, Processing: {6}",
                miningSiteId, total, on, off, red, orange, blue);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (!string.IsNullOrEmpty(miningSiteId))
            {
                string topic = $"sites/{miningSiteId}/disconnect";
                string payload = "{\"siteId\":\"" + miningSiteId + "\",\"status\":\"disconnected\"}";
                MqttHandler.PublishMessage(topic, payload);
            }
        }
    }
}