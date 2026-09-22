using System.Windows;
using System.IO;
using System.Windows.Threading;

namespace GeosangHub;

public partial class App : System.Windows.Application
{
    private bool _errorShown;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("Background task", e.Exception);
            e.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("UI", e.Exception);
        e.Handled = true;
        if (_errorShown) return;
        _errorShown = true;
        System.Windows.MessageBox.Show("화면 처리 중 오류가 발생했지만 프로그램은 계속 실행됩니다.\n오류 기록: %LOCALAPPDATA%\\GeosangIntegratedHub\\error.log",
            "거상 통합 도우미", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }

    private static void Log(string area, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(HubSettings.Folder);
            File.AppendAllText(Path.Combine(HubSettings.Folder, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {area}\n{ex}\n\n");
        }
        catch { }
    }
}
