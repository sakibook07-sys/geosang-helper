using System.Windows;
using System.IO;
using LibreHardwareMonitor.Hardware;
using UserControl = System.Windows.Controls.UserControl;

namespace GeosangHub;

public partial class CpuTemperaturePane : UserControl, IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private bool _started;
    private bool _disposed;
    private bool _loggedFirstReading;
    private bool _firstReadingEmpty;
    private bool _loggedRecovery;

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
        while (!token.IsCancellationRequested)
        {
            Computer? computer = null;
            try
            {
                computer = new Computer { IsCpuEnabled = true };
                computer.Open();
                int emptyCount = 0;
                while (!token.IsCancellationRequested)
                {
                    var readings = new List<CpuReading>();
                    var rawTemperatures = new List<string>();
                    var cpuNames = new List<string>();
                    foreach (IHardware hardware in computer.Hardware)
                    {
                        if (hardware.HardwareType != HardwareType.Cpu) continue;
                        cpuNames.Add(hardware.Name);
                        CollectReadings(hardware, readings, rawTemperatures);
                    }

                    var ordered = readings.OrderBy(r => SensorPriority(r.Name)).ThenBy(r => r.Name).ToArray();
                    string cpuName = cpuNames.Count == 0 ? "CPU를 찾지 못했습니다." : string.Join(" · ", cpuNames);
                    string sensorDetails = rawTemperatures.Count == 0 ? "온도 센서 없음" : string.Join(", ", rawTemperatures);
                    if (!_loggedFirstReading)
                    {
                        LogSensorState(cpuName, sensorDetails);
                        _firstReadingEmpty = ordered.Length == 0;
                        _loggedFirstReading = true;
                    }
                    else if (_firstReadingEmpty && !_loggedRecovery && ordered.Length > 0)
                    {
                        LogSensorState("센서 연결 성공", sensorDetails);
                        _loggedRecovery = true;
                    }
                    if (!_disposed)
                        await Dispatcher.InvokeAsync(() => ShowReadings(cpuName, ordered, sensorDetails));
                    emptyCount = ordered.Length == 0 ? emptyCount + 1 : 0;
                    if (emptyCount >= 3) break;
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                LogSensorState("센서 오류", ex.ToString());
                if (!_disposed)
                    await Dispatcher.InvokeAsync(() => ShowError(ex.Message));
            }
            finally
            {
                try { computer?.Close(); } catch { }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(10), token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
        }
    }

    private static void CollectReadings(IHardware hardware, List<CpuReading> readings, List<string> rawTemperatures)
    {
        hardware.Update();
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature) continue;
            rawTemperatures.Add($"{sensor.Name}={sensor.Value?.ToString("0.0") ?? "값 없음"}");
            if (sensor.Value is not float value || !float.IsFinite(value) || value <= 0 || value > 130) continue;
            readings.Add(new CpuReading(sensor.Name, value));
        }
        foreach (IHardware subHardware in hardware.SubHardware)
            CollectReadings(subHardware, readings, rawTemperatures);
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

    private void ShowReadings(string cpuName, CpuReading[] readings, string sensorDetails)
    {
        if (_disposed) return;
        if (readings.Length == 0)
        {
            TemperatureText.Text = "-- °C";
            StatusText.Text = "측정 불가";
            ToolTip = cpuName + "\n현재 센서값: " + sensorDetails + "\n센서를 다시 연결해 보겠습니다. 관리자 권한에서도 값이 없으면 센서 로그를 확인하세요.";
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

    private static void LogSensorState(string cpuName, string sensorDetails)
    {
        try
        {
            Directory.CreateDirectory(HubSettings.Folder);
            File.AppendAllText(Path.Combine(HubSettings.Folder, "cpu-sensor.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {cpuName}: {sensorDetails}{Environment.NewLine}");
        }
        catch { }
    }

    public void Dispose()
    {
        _disposed = true;
        _stop.Cancel();
    }

    private sealed record CpuReading(string Name, float Value);
}
