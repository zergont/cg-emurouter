using System.Collections.Concurrent;
using CgEmulator.Config;
using MQTTnet.Client;

namespace CgEmulator.Mqtt;

public sealed class ReplayBuffer
{
    private readonly ConcurrentQueue<BufferedMqttMessage> _queue = new();
    private readonly ReplayConfig _config;
    private readonly ILogger<ReplayBuffer> _logger;
    private int _count;
    private long _droppedCount;

    public ReplayBuffer(ReplayConfig config, ILogger<ReplayBuffer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public int Count => Volatile.Read(ref _count);

    public int MaxSize => _config.BufferMaxSize;

    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public void Enqueue(BufferedMqttMessage message)
    {
        var maxSize = Math.Max(1, _config.BufferMaxSize);

        while (Count >= maxSize && _queue.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
            var dropped = Interlocked.Increment(ref _droppedCount);
            _logger.LogWarning("[Replay] Buffer overflow. Dropping oldest message. Buffered={Buffered}, Max={Max}, DroppedTotal={DroppedTotal}", Count, maxSize, dropped);
        }

        _queue.Enqueue(message);
        Interlocked.Increment(ref _count);
    }

    public async Task DrainAsync(IMqttClient client, ReplayConfig config, CancellationToken ct)
    {
        var buffered = Count;
        if (buffered <= 0)
        {
            return;
        }

        var rate = Math.Max(0, config.RatePerSec);
        _logger.LogInformation("[Replay] Draining {Count} messages at {Rate} msg/sec", buffered, rate);
        var delay = rate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(1d / rate);
        var sent = 0;

        while (!ct.IsCancellationRequested && _queue.TryDequeue(out var message))
        {
            Interlocked.Decrement(ref _count);

            try
            {
                await client.PublishAsync(message.Message, ct);
            }
            catch
            {
                Enqueue(message);
                throw;
            }

            sent++;
            if (sent % 10 == 0 || Count == 0)
            {
                _logger.LogInformation("[Replay] Progress: sent={Sent}, remaining={Remaining}", sent, Count);
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }
        }

        _logger.LogInformation("[Replay] Drain finished. Sent={Sent}, Remaining={Remaining}, DroppedTotal={DroppedTotal}", sent, Count, DroppedCount);
    }
}

public sealed class BufferedMqttMessage
{
    public BufferedMqttMessage(MQTTnet.MqttApplicationMessage message)
    {
        Message = message;
    }

    public MQTTnet.MqttApplicationMessage Message { get; }
}
