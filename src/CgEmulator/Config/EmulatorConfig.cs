namespace CgEmulator.Config;

public sealed class EmulatorConfig
{
    public WebConfig Web { get; set; } = new();
    public MqttConfig Mqtt { get; set; } = new();
    public DefaultsConfig Defaults { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public sealed class WebConfig
{
    public string BindIp { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 6666;
}

public sealed class MqttConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 1883;
    public string TopicPrefix { get; set; } = "cg/v1/telemetry/SN";
    public string ClientIdPrefix { get; set; } = "cg-emulator";
    public string Protocol { get; set; } = "v5";
    public int Qos { get; set; } = 0;
}

public sealed class DefaultsConfig
{
    public int GpsPeriodSec { get; set; } = 60;
    public int EquipmentPeriodSec { get; set; } = 5;
    public int GpsDriftM { get; set; } = 300;
    public int RfAnchorOffsetKm { get; set; } = 50;
}

public sealed class LoggingConfig
{
    public string Level { get; set; } = "Information";
}
