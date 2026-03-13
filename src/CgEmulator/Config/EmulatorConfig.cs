namespace CgEmulator.Config;

public sealed class EmulatorConfig
{
    public WebConfig Web { get; set; } = new();
    public MqttConfig Mqtt { get; set; } = new();
    public ReplayConfig Replay { get; set; } = new();
    public DefaultsConfig Defaults { get; set; } = new();
    public SimConfig Sim { get; set; } = new();
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

public sealed class ReplayConfig
{
    public int RatePerSec { get; set; } = 10;
    public int BufferMaxSize { get; set; } = 1000;
}

public sealed class SimConfig
{
    // Equipment initialisation ranges
    public int NominalPowerMinKw { get; set; } = 5000;
    public int NominalPowerMaxKw { get; set; } = 20000;
    public int NominalPowerStepKw { get; set; } = 1000;
    public int EngineTimeInitialMax { get; set; } = 10_000_000;
    public int ControllerTimeInitialMax { get; set; } = 10_000;

    // State-machine durations (seconds)
    public int State0MinSec { get; set; } = 1800;
    public int State0MaxSec { get; set; } = 7200;
    public int State1DurSec { get; set; } = 60;
    public int State2DurSec { get; set; } = 60;
    public int State3DurSec { get; set; } = 900;
    public int State4MinSec { get; set; } = 7200;
    public int State4MaxSec { get; set; } = 172800;
    public int State5DurSec { get; set; } = 1800;
    public int State6DurSec { get; set; } = 60;

    // Load model
    public double LoadMinFactor { get; set; } = 0.3;
    public double LoadMaxFactor { get; set; } = 0.9;
    public int LoadHoldMinSec { get; set; } = 60;
    public int LoadHoldMaxSec { get; set; } = 600;

    // GPS
    public int SatellitesMin { get; set; } = 3;
    public int SatellitesMax { get; set; } = 12;
}

public sealed class LoggingConfig
{
    public string Level { get; set; } = "Information";
}
