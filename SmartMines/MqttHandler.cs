using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace SmartMines
{
    public static class MqttHandler
    {
        private static IMqttClient mqttClient;
        private static Action<string, string> externalMessageHandler;
        public static readonly string clientId = "smartmine-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        public static async Task ConnectAsync(Action<string, string> onMessageReceived)
        {
            externalMessageHandler = onMessageReceived;

            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer("24f64a7a615e459798b1f62a8a9fbb5d.s1.eu.hivemq.cloud", 8883)
                .WithCredentials("hivemq.webclient.1743136386898", "7VNDB$tm:frq8Y0S*a#1")
                .WithTlsOptions(o =>
                {
                    o.UseTls(true);
                    o.WithAllowUntrustedCertificates(true);
                    o.WithIgnoreCertificateChainErrors(false);
                    o.WithIgnoreCertificateRevocationErrors(false);
                })
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .Build();

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = Encoding.UTF8.GetString(
                    e.ApplicationMessage.PayloadSegment.Array,
                    e.ApplicationMessage.PayloadSegment.Offset,
                    e.ApplicationMessage.PayloadSegment.Count
                );

                Console.WriteLine($"[MQTT RECEIVED]\nTopic: {topic}\nPayload: {payload}");

                externalMessageHandler?.Invoke(topic, payload);
                return Task.CompletedTask;
            };

            await mqttClient.ConnectAsync(options, CancellationToken.None);

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic("sites/+/sections/+/lights/+")
                .WithAtLeastOnceQoS()
                .Build());

            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic("sites/+/sections/+/lights")
                .WithAtLeastOnceQoS()
                .Build());
        }

        public static async void PublishMessage(string topic, string payload)
        {
            if (mqttClient != null && mqttClient.IsConnected)
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await mqttClient.PublishAsync(message);
            }
        }

        public static void PublishSectionStatus(string siteId, int sectionId, List<string> lightStatuses)
        {
            if (mqttClient != null && mqttClient.IsConnected)
            {
                var lights = new List<object>();
                foreach (var entry in lightStatuses)
                {
                    var parts = entry.Split('=');
                    var idStr = parts[0].Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries)[1].Replace("light", "");
                    var state = parts[1];
                    int id;
                    if (int.TryParse(idStr, out id))
                    {
                        lights.Add(new { id, state });
                    }
                }

                var payload = JsonConvert.SerializeObject(new
                {
                    siteId = siteId,
                    section = sectionId,
                    sender = clientId,
                    lights = lights
                });

                var topic = $"sites/{siteId}/sections/{sectionId}/lights";
                PublishMessage(topic, payload);
            }
        }

        public static void PublishAllSectionsStatus(string siteId, List<string> allStatuses)
        {
            if (mqttClient != null && mqttClient.IsConnected)
            {
                var lights = new List<object>();
                foreach (var entry in allStatuses)
                {
                    var parts = entry.Split('=');
                    var segment = parts[0].Split('/');
                    var sectionId = int.Parse(segment[0].Replace("section", ""));
                    var lightId = int.Parse(segment[1].Replace("light", ""));
                    var state = parts[1];

                    lights.Add(new
                    {
                        section = sectionId,
                        id = lightId,
                        state = state
                    });
                }

                var payload = JsonConvert.SerializeObject(new
                {
                    siteId = siteId,
                    sender = clientId,
                    lights = lights
                });

                var topic = $"sites/{siteId}/all";
                PublishMessage(topic, payload);
            }
        }


        public static void PublishLightStatus(string siteId, int sectionId, int lightId, string state)
        {
            if (mqttClient != null && mqttClient.IsConnected)
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    siteId = siteId,
                    section = sectionId,
                    id = lightId,
                    state = state,
                    sender = clientId
                });

                var topic = $"sites/{siteId}/sections/{sectionId}/lights/{lightId}";
                PublishMessage(topic, payload);
            }
        }
    }
}
