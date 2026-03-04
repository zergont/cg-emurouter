using System.Text;
using System.Text.Json;
using CgEmulator.Config;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace CgEmulator.Mqtt;

public sealed class MqttPublisher : IAsyncDisposable
{
    private readonly EmulatorConfig _config;
    private readonly IMqttClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public MqttPublisher(EmulatorConfig config)
    {
        _config = config;
        _client = new MqttFactory().CreateMqttClient();
    }

    public async Task PublishAsync(string topic, object payload, CancellationToken cancellationToken)
    {
        await EnsureConnected(cancellationToken);

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(json))
            .WithQualityOfServiceLevel(MapQos(_config.Mqtt.Qos))
            .Build();

        await _client.PublishAsync(message, cancellationToken);
    }

    private async Task EnsureConnected(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return;
        }

        var builder = new MqttClientOptionsBuilder()
            .WithClientId($"{_config.Mqtt.ClientIdPrefix}-{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 23))
            .WithTcpServer(_config.Mqtt.Host, _config.Mqtt.Port)
            .WithCleanSession();

        if (string.Equals(_config.Mqtt.Protocol, "v5", StringComparison.OrdinalIgnoreCase))
        {
            builder.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);
        }
        else
        {
            builder.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311);
        }

        await _client.ConnectAsync(builder.Build(), cancellationToken);
    }

    private static MqttQualityOfServiceLevel MapQos(int qos)
    {
        return qos switch
        {
            1 => MqttQualityOfServiceLevel.AtLeastOnce,
            2 => MqttQualityOfServiceLevel.ExactlyOnce,
            _ => MqttQualityOfServiceLevel.AtMostOnce
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }

        _client.Dispose();
    }
}
