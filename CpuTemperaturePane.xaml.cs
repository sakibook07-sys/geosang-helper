using System.Windows;
using LibreHardwareMonitor.Hardware;
using UserControl = System.Windows.Controls.UserControl;

namespace GeosangHub;

public partial class CpuTemperaturePane : UserControl, IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private bool _started;
    private bool _disposed;

    public CpuTemperaturePane()
    {
        InitializeComponent();
        Loaded += (_, _) => Start();
    }

    private void Start()
    {
        if (_started || _disposed) return;
        _started = true;
        _ = Task.Run(() => ReadLoopAsync(_stop.Token));
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        Computer? computer = null;
        try
        {
            computer = new Computer { IsCpuEnabled = true };
            computer.Open();
            while (!token.IsCancellationRequested)
            {
                var readings = new List<CpuReading>();
                var cpuNames = new List<string>();
                foreach (IHardware hardware in computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Cpu) continue;
                    cpuNames.Add(hardware.Name);
                    CollectReadings(hardware, readings);
                }

                var ordered = readings.OrderBy(r => SensorPriority(r.Name)).ThenBy(r => r.Name).ToArray();
                string cpuName = cpuNames.Count == 0 ? "CPU를 찾지 못했습니다." : string.Join(" · ", cpuNames);
                if (!_disposed)
                    await Dispatcher.InvokeAsync(() => ShowReadings(cpuName, ordered));
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_disposed)
                await Dispatcher.InvokeAsync(() => ShowError(ex.Message));
        }
        finally
        {
            try { computer?.Close(); } catch { }
        }
    }

    private static void CollectReadings(IHardware hardware, List<CpuReading> readings)
    {
        hardware.Update();
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || sensor.Value is not float value
                || !float.IsFinite(value) || value <= 0 || value > 130) continue;
            readings.Add(new CpuReading(sensor.Name, value));
        }
        foreach (IHardware subHardware in hardware.SubHardware)
            CollectReadings(subHardware, readings);
    }

    private static int SensorPriority(string name)
    {
        if (name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.Contains("Average", StringComparison.OrdinalIgnoreCase)) return 2;
        if (name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)) return 3;
        if (name.Contains("Core", StringComparison.OrdinalIgnoreCase)) return 4;
        return 5;
    }

    private void ShowReadings(string cpuName, CpuReading[] readings)
    {
        if (_disposed) return;
        if (readings.Length == 0)
        {
            TemperatureText.Text = "-- °C";
            StatusText.Text = "측정 불가";
            ToolTip = cpuName + "\n유효한 온도 값이 없습니다. 관리자 권한이 필요하거나 이 PC의 센서가 지원되지 않을 수 있습니다.";
            return;
        }

        var primary = readings[0];
        TemperatureText.Text = $"{primary.Value:0.0} °C";
        StatusText.Text = primary.Name;
        ToolTip = cpuName + $"\n{DateTime.Now:HH:mm:ss} 갱신\n" +
            string.Join("\n", readings.Take(8).Select(r => $"{r.Name}: {r.Value:0.0} °C"));
    }

    private void ShowError(string message)
    {
        if (_disposed) return;
        TemperatureText.Text = "-- °C";
        StatusText.Text = "센서 오류";
        ToolTip = message + "\n필요한 경우 앱을 관리자 권한으로 다시 실행해보세요.";
    }

    public void Dispose()
    {
        _disposed = true;
        _stop.Cancel();
    }

    private sealed record CpuReading(string Name, float Value);
}
