using System.Text.Json.Serialization;

namespace CgEmulator.Sim;

public sealed class EquipmentSimulator
{
    private readonly Random _random;
    private int _state;
    private int _secondsInStateRemaining;
    private int _targetKw;
    private int _targetHoldRemaining;
    private double _counterAccumulator;

    public EquipmentSimulator(int serverId, Random random)
    {
        ServerId = serverId;
        _random = random;
        NominalPower3019 = random.Next(5, 21) * 1000;
        EngineRunningTime70 = (uint)random.NextInt64(0, 10_000_001);
        ControllerOnTime290 = (uint)random.Next(0, 10001);
        _state = 0;
        _secondsInStateRemaining = NextStateDuration(_state);
    }

    public int ServerId { get; }
    public int NominalPower3019 { get; }
    public uint EngineRunningTime70 { get; private set; }
    public uint ControllerOnTime290 { get; private set; }
    public int State6109 => _state;
    public int Load34Kw { get; private set; }
    public int SecondsToTransition => _secondsInStateRemaining;

    public void Advance(double elapsedSeconds, int equipmentPeriodSec)
    {
        if (elapsedSeconds <= 0)
        {
            return;
        }

        _counterAccumulator += elapsedSeconds;
        var wholeSeconds = (int)_counterAccumulator;
        _counterAccumulator -= wholeSeconds;

        if (wholeSeconds > 0)
        {
            ControllerOnTime290 = unchecked(ControllerOnTime290 + (uint)wholeSeconds);
            if (_state == 4)
            {
                EngineRunningTime70 = unchecked(EngineRunningTime70 + (uint)(wholeSeconds * 10));
            }

            AdvanceStateMachine(wholeSeconds);
        }

        UpdateLoad(equipmentPeriodSec);
    }

    private void AdvanceStateMachine(int seconds)
    {
        var remaining = seconds;
        while (remaining > 0)
        {
            if (remaining < _secondsInStateRemaining)
            {
                _secondsInStateRemaining -= remaining;
                return;
            }

            remaining -= _secondsInStateRemaining;
            _state = (_state + 1) % 7;
            _secondsInStateRemaining = NextStateDuration(_state);

            if (_state == 4)
            {
                Load34Kw = Math.Clamp(Load34Kw, 0, NominalPower3019);
                PickNextTarget();
            }

            if (_state != 4)
            {
                _targetHoldRemaining = 0;
                _targetKw = 0;
            }
        }
    }

    private int NextStateDuration(int state)
    {
        return state switch
        {
            0 => _random.Next(1800, 7201),
            1 => 60,
            2 => 60,
            3 => 900,
            4 => _random.Next(7200, 172801),
            5 => 1800,
            6 => 60,
            _ => 60
        };
    }

    private void UpdateLoad(int equipmentPeriodSec)
    {
        if (_state != 4)
        {
            Load34Kw = 0;
            return;
        }

        if (_targetHoldRemaining > 0)
        {
            _targetHoldRemaining -= equipmentPeriodSec;
        }

        if (_targetHoldRemaining <= 0 && Load34Kw == _targetKw)
        {
            PickNextTarget();
        }

        var maxDelta = Math.Max(1, equipmentPeriodSec * 5);
        var delta = _targetKw - Load34Kw;
        if (delta > maxDelta)
        {
            delta = maxDelta;
        }
        else if (delta < -maxDelta)
        {
            delta = -maxDelta;
        }

        Load34Kw += delta;

        if (Load34Kw == _targetKw && _targetHoldRemaining <= 0)
        {
            _targetHoldRemaining = _random.Next(60, 601);
        }
    }

    private void PickNextTarget()
    {
        var min = (int)Math.Round(NominalPower3019 * 0.03);
        var max = (int)Math.Round(NominalPower3019 * 0.09);
        _targetKw = _random.Next(min, max + 1);
    }

    public int[] GetCounter70Words()
    {
        return [(int)(EngineRunningTime70 >> 16), (int)(EngineRunningTime70 & 0xFFFF)];
    }

    public int[] GetCounter290Words()
    {
        return [(int)(ControllerOnTime290 >> 16), (int)(ControllerOnTime290 & 0xFFFF)];
    }
}

public sealed class EquipmentStateDto
{
    [JsonPropertyName("server_id")]
    public int ServerId { get; set; }

    [JsonPropertyName("3019")]
    public int Register3019 { get; set; }

    [JsonPropertyName("6109")]
    public int Register6109 { get; set; }

    [JsonPropertyName("sec_to_transition")]
    public int SecToTransition { get; set; }

    [JsonPropertyName("34")]
    public int Register34 { get; set; }

    [JsonPropertyName("70")]
    public uint Register70 { get; set; }

    [JsonPropertyName("290")]
    public uint Register290 { get; set; }
}
