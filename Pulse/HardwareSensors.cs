using LibreHardwareMonitor.Hardware;

namespace Pulse;

/// <summary>Optional sensor reader. A value can be absent when firmware or hardware does not expose it.</summary>
internal sealed class HardwareSensors : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMotherboardEnabled = true,
    };

    public HardwareSensors() => _computer.Open();

    public SensorReading Read()
    {
        var cpu = new List<(string Name, float Value)>();
        var gpu = new List<(string Name, float Value)>();
        foreach (var hardware in _computer.Hardware)
        {
            ReadHardware(hardware, cpu, gpu);
        }
        return new SensorReading(SelectCpuTemperature(cpu), gpu.Count == 0 ? null : gpu.Max(x => x.Value));
    }

    private static void ReadHardware(IHardware hardware, List<(string Name, float Value)> cpu, List<(string Name, float Value)> gpu)
    {
        try { hardware.Update(); } catch { return; }
        var target = hardware.HardwareType == HardwareType.Cpu ? cpu
            : hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel ? gpu
            : null;
        if (target is not null)
        {
            foreach (var sensor in hardware.Sensors)
                if (sensor.SensorType == SensorType.Temperature && sensor.Value is float value && value is > 0 and < 150)
                    target.Add((sensor.Name, value));
        }
        foreach (var child in hardware.SubHardware) ReadHardware(child, cpu, gpu);
    }

    private static float? SelectCpuTemperature(List<(string Name, float Value)> values)
    {
        if (values.Count == 0) return null;
        var preferred = values.FirstOrDefault(x => x.Name.Contains("package", StringComparison.OrdinalIgnoreCase)
            || x.Name.Contains("tctl", StringComparison.OrdinalIgnoreCase)
            || x.Name.Contains("die", StringComparison.OrdinalIgnoreCase));
        return preferred.Name is not null ? preferred.Value : values.Max(x => x.Value);
    }

    public void Dispose() => _computer.Close();
}

internal sealed record SensorReading(float? CpuCelsius, float? GpuCelsius);
