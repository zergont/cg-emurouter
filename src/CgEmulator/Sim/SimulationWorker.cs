using CgEmulator.Mqtt;

namespace CgEmulator.Sim;

public sealed class SimulationWorker : BackgroundService
{
    private readonly SimulationManager _simulation;
    private readonly MqttPublisher _publisher;
    private readonly ILogger<SimulationWorker> _logger;

    public SimulationWorker(SimulationManager simulation, MqttPublisher publisher, ILogger<SimulationWorker> logger)
    {
        _simulation = simulation;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var previous = DateTimeOffset.UtcNow;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = (now - previous).TotalSeconds;
            previous = now;

            var messages = _simulation.Tick(elapsed, now);
            foreach (var message in messages)
            {
                try
                {
                    await _publisher.PublishAsync(message.Topic, message.Payload, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MQTT publish failed for topic {Topic}", message.Topic);
                }
            }
        }
    }
}
