using System.Text.Json.Serialization;
using CgEmulator.Config;

namespace CgEmulator.Sim;

public sealed class SimulationManager
{
    private readonly EmulatorConfig _config;
    private readonly object _sync = new();
    private readonly Random _random = new();
    private readonly List<ObjectSimulator> _objects = [];

    public SimulationManager(EmulatorConfig config)
    {
        _config = config;
        CurrentEquipmentPeriodSec = config.Defaults.EquipmentPeriodSec;
    }

    public bool IsRunning { get; private set; }
    public int CurrentEquipmentPeriodSec { get; private set; }

    public StateSnapshot GetState()
    {
        lock (_sync)
        {
            return new StateSnapshot
            {
                IsRunning = IsRunning,
                EquipmentPeriodSec = CurrentEquipmentPeriodSec,
                Objects = _objects.Select(o => o.ToState(IsRunning)).ToList()
            };
        }
    }

    public StateSnapshot Recreate(CreateObjectsRequest request)
    {
        lock (_sync)
        {
            var objectCount = Math.Max(0, request.ObjectCount);
            var minEquip = Math.Max(1, request.MinEquip);
            var maxEquip = Math.Max(minEquip, request.MaxEquip);
            var period = request.EquipmentPeriodSec <= 0 ? _config.Defaults.EquipmentPeriodSec : request.EquipmentPeriodSec;

            CurrentEquipmentPeriodSec = period;
            _objects.Clear();

            for (var i = 0; i < objectCount; i++)
            {
                var point = GenerateRussiaPoint(_config.Defaults.RfAnchorOffsetKm);
                var equipmentCount = _random.Next(minEquip, maxEquip + 1);
                var serial = GenerateSerial();
                _objects.Add(new ObjectSimulator(serial, point.Lat, point.Lon, equipmentCount, period, _config, _random));
            }

            IsRunning = false;
            return GetState();
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            IsRunning = false;
        }
    }

    public List<OutboundMessage> Tick(double elapsedSeconds, DateTimeOffset utcNow)
    {
        lock (_sync)
        {
            var messages = new List<OutboundMessage>();
            foreach (var obj in _objects)
            {
                messages.AddRange(obj.Tick(elapsedSeconds, IsRunning, utcNow));
            }

            return messages;
        }
    }

    private (double Lat, double Lon) GenerateRussiaPoint(int maxOffsetKm)
    {
        var anchor = GeoAnchors.Russia[_random.Next(0, GeoAnchors.Russia.Length)];
        var radiusM = Math.Sqrt(_random.NextDouble()) * maxOffsetKm * 1000d;
        var angle = _random.NextDouble() * 2 * Math.PI;

        var dLat = radiusM * Math.Cos(angle) / 111_320d;
        var dLon = radiusM * Math.Sin(angle) / (111_320d * Math.Cos(anchor.Lat * Math.PI / 180d));

        return (anchor.Lat + dLat, anchor.Lon + dLon);
    }

    private string GenerateSerial()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> suffix = stackalloc char[5];

        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = chars[_random.Next(chars.Length)];
        }

        return $"EMU{new string(suffix)}";
    }
}

public sealed class CreateObjectsRequest
{
    [JsonPropertyName("object_count")]
    public int ObjectCount { get; set; }

    [JsonPropertyName("min_equip")]
    public int MinEquip { get; set; }

    [JsonPropertyName("max_equip")]
    public int MaxEquip { get; set; }

    [JsonPropertyName("equipment_period_sec")]
    public int EquipmentPeriodSec { get; set; }
}

public sealed class StateSnapshot
{
    [JsonPropertyName("is_running")]
    public bool IsRunning { get; set; }

    [JsonPropertyName("equipment_period_sec")]
    public int EquipmentPeriodSec { get; set; }

    [JsonPropertyName("objects")]
    public List<ObjectStateDto> Objects { get; set; } = [];
}
