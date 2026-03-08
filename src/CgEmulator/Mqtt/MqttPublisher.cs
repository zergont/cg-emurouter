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
    private readonly ReplayBuffer _replayBuffer;
    private readonly ILogger<MqttPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private readonly object _replaySync = new();
    private Task? _replayTask;
    private volatile bool _isReplaying;

    public MqttPublisher(EmulatorConfig config, ReplayBuffer replayBuffer, ILogger<MqttPublisher> logger)
    {
        _config = config;
        _replayBuffer = replayBuffer;
        _logger = logger;
        _client = new MqttFactory().CreateMqttClient();
    }

    public async Task PublishAsync(string topic, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(json))
            .WithQualityOfServiceLevel(MapQos(_config.Mqtt.Qos))
            .Build();

        if (_isReplaying || _replayBuffer.Count > 0)
        {
            _replayBuffer.Enqueue(new BufferedMqttMessage(message));
            EnsureReplayLoopStarted();
            return;
        }

        try
        {
            await EnsureConnected(cancellationToken);
            await _client.PublishAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MQTT publish failed. Message moved to replay buffer.");
            _replayBuffer.Enqueue(new BufferedMqttMessage(message));
            EnsureReplayLoopStarted();
        }
    }

    public ReplayStatus GetReplayStatus()
    {
        return new ReplayStatus
        {
            Buffered = _replayBuffer.Count,
            Max = _replayBuffer.MaxSize,
            Replaying = _isReplaying,
            Rate = _config.Replay.RatePerSec,
            DroppedTotal = _replayBuffer.DroppedCount
        };
    }

    private void EnsureReplayLoopStarted()
    {
        lock (_replaySync)
        {
            if (_replayTask is { IsCompleted: false })
            {
                return;
            }

            _logger.LogInformation("[Replay] Mode switch: Live -> Replaying. Buffered={Buffered}, Rate={Rate}", _replayBuffer.Count, _config.Replay.RatePerSec);
            _replayTask = Task.Run(ReplayLoopAsync);
        }
    }

    private async Task ReplayLoopAsync()
    {
        _isReplaying = true;

        try
        {
            while (_replayBuffer.Count > 0)
            {
                try
                {
                    await EnsureConnected(CancellationToken.None);
                    await _replayBuffer.DrainAsync(_client, _config.Replay, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Replay] Paused due to MQTT connection error. Remaining={Remaining}", _replayBuffer.Count);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
        finally
        {
            _isReplaying = false;
            _logger.LogInformation("[Replay] Mode switch: Replaying -> Live. Buffered={Buffered}, DroppedTotal={DroppedTotal}", _replayBuffer.Count, _replayBuffer.DroppedCount);
        }
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
        if (_replayTask is not null)
        {
            await _replayTask.WaitAsync(TimeSpan.FromSeconds(1));
        }

        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }

        _client.Dispose();
    }
}

public sealed class ReplayStatus
{
    public int Buffered { get; set; }
    public int Max { get; set; }
    public bool Replaying { get; set; }
    public int Rate { get; set; }
    public long DroppedTotal { get; set; }
}
