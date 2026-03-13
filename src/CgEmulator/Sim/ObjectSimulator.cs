using System.Globalization;
using System.Text.Json.Serialization;
using CgEmulator.Config;
using CgEmulator.Time;

namespace CgEmulator.Sim;

public sealed class ObjectSimulator
{
    private readonly Random _random;
    private readonly EmulatorConfig _config;
    private readonly List<EquipmentSimulator> _equipment;
    private double _gpsCountdown;
    private double _pccCountdown;

    public ObjectSimulator(string serialNumber, double fixedLat, double fixedLon, int equipmentCount, int equipmentPeriodSec, EmulatorConfig config, Random random)
    {
        SerialNumber = serialNumber;
        FixedLatitude = fixedLat;
        FixedLongitude = fixedLon;
        CurrentLatitude = fixedLat;
        CurrentLongitude = fixedLon;
        EquipmentPeriodSec = equipmentPeriodSec;
        _config = config;
        _random = random;
        _equipment = Enumerable.Range(1, equipmentCount).Select(i => new EquipmentSimulator(i, random, config.Sim)).ToList();
        _gpsCountdown = config.Defaults.GpsPeriodSec;
        _pccCountdown = equipmentPeriodSec;
    }

    public string SerialNumber { get; }
    public double FixedLatitude { get; }
    public double FixedLongitude { get; }
    public double CurrentLatitude { get; private set; }
    public double CurrentLongitude { get; private set; }
    public int EquipmentPeriodSec { get; }
    public IReadOnlyList<EquipmentSimulator> Equipment => _equipment;

    public List<OutboundMessage> Tick(double elapsedSeconds, bool running, DateTimeOffset utcNow)
    {
        var result = new List<OutboundMessage>();
        if (!running)
        {
            return result;
        }

        foreach (var eq in _equipment)
        {
            eq.Advance(elapsedSeconds, EquipmentPeriodSec);
        }

        _gpsCountdown -= elapsedSeconds;
        _pccCountdown -= elapsedSeconds;

        while (_gpsCountdown <= 0)
        {
            _gpsCountdown += _config.Defaults.GpsPeriodSec;
            result.Add(BuildGpsMessage(utcNow));
        }

        while (_pccCountdown <= 0)
        {
            _pccCountdown += EquipmentPeriodSec;
            result.Add(BuildPccMessage(utcNow));
        }

        return result;
    }

    private OutboundMessage BuildGpsMessage(DateTimeOffset utcNow)
    {
        var drifted = DriftPoint(FixedLatitude, FixedLongitude, _config.Defaults.GpsDriftM);
        CurrentLatitude = drifted.Lat;
        CurrentLongitude = drifted.Lon;

        var msk = Clock.ToMsk(utcNow);

        var payload = new
        {
            GPS = new
            {
                latitude = Math.Round(CurrentLatitude, 6),
                longitude = Math.Round(CurrentLongitude, 6),
                satellites = _random.Next(_config.Sim.SatellitesMin, _config.Sim.SatellitesMax + 1),
                fix_status = 1,
                timestamp = utcNow.ToUnixTimeSeconds(),
                date = msk.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                date_iso_8601 = msk.ToString("yyyy-MM-dd'T'HH:mm:sszzzz", CultureInfo.InvariantCulture).Replace(":", string.Empty)
            }
        };

        return new OutboundMessage(GetTopic(), payload);
    }

    private OutboundMessage BuildPccMessage(DateTimeOffset utcNow)
    {
        var msk = Clock.ToMsk(utcNow);
        var iso = msk.ToString("yyyy-MM-dd'T'HH:mm:sszzzz", CultureInfo.InvariantCulture).Replace(":", string.Empty);

        var rows = new List<object>(_equipment.Count * 5);

        foreach (var eq in _equipment)
        {
            rows.Add(new { date_iso_8601 = iso, server_id = eq.ServerId, addr = 6109, data = $"[{eq.State6109}]" });
            rows.Add(new { date_iso_8601 = iso, server_id = eq.ServerId, addr = 34, data = $"[{eq.Load34Kw}]" });
            rows.Add(new { date_iso_8601 = iso, server_id = eq.ServerId, addr = 3019, data = $"[{eq.NominalPower3019}]" });

            var r70 = eq.GetCounter70Words();
            rows.Add(new { date_iso_8601 = iso, server_id = eq.ServerId, addr = 70, data = $"[{r70[0]},{r70[1]}]" });

            var r290 = eq.GetCounter290Words();
            rows.Add(new { date_iso_8601 = iso, server_id = eq.ServerId, addr = 290, data = $"[{r290[0]},{r290[1]}]" });
        }

        var payload = new { PCC_3_3 = rows };
        return new OutboundMessage(GetTopic(), payload);
    }

    private string GetTopic()
    {
        return $"{_config.Mqtt.TopicPrefix}/{SerialNumber}";
    }

    private (double Lat, double Lon) DriftPoint(double lat, double lon, double maxRadiusM)
    {
        var radius = Math.Sqrt(_random.NextDouble()) * maxRadiusM;
        var angle = _random.NextDouble() * 2 * Math.PI;

        var dLat = radius * Math.Cos(angle) / 111_320d;
        var dLon = radius * Math.Sin(angle) / (111_320d * Math.Cos(lat * Math.PI / 180d));

        return (lat + dLat, lon + dLon);
    }

    public ObjectStateDto ToState(bool running)
    {
        return new ObjectStateDto
        {
            SerialNumber = SerialNumber,
            FixedLatitude = Math.Round(FixedLatitude, 6),
            FixedLongitude = Math.Round(FixedLongitude, 6),
            CurrentLatitude = Math.Round(CurrentLatitude, 6),
            CurrentLongitude = Math.Round(CurrentLongitude, 6),
            EquipmentCount = _equipment.Count,
            Status = running ? "РАБОТАЕТ" : "ОСТАНОВЛЕНО",
            Equipment = _equipment.Select(eq => new EquipmentStateDto
            {
                ServerId = eq.ServerId,
                Register3019 = eq.NominalPower3019,
                Register6109 = eq.State6109,
                SecToTransition = eq.SecondsToTransition,
                Register34 = eq.Load34Kw,
                Register70 = eq.EngineRunningTime70,
                Register290 = eq.ControllerOnTime290
            }).ToList()
        };
    }
}

public sealed class OutboundMessage(string topic, object payload)
{
    public string Topic { get; } = topic;
    public object Payload { get; } = payload;
}

public sealed class ObjectStateDto
{
    [JsonPropertyName("sn")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("fixed_lat")]
    public double FixedLatitude { get; set; }

    [JsonPropertyName("fixed_lon")]
    public double FixedLongitude { get; set; }

    [JsonPropertyName("current_lat")]
    public double CurrentLatitude { get; set; }

    [JsonPropertyName("current_lon")]
    public double CurrentLongitude { get; set; }

    [JsonPropertyName("equipment_count")]
    public int EquipmentCount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ОСТАНОВЛЕНО";

    [JsonPropertyName("equipment")]
    public List<EquipmentStateDto> Equipment { get; set; } = [];
}
