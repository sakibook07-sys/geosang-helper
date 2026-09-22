using Microsoft.Win32;
namespace GeosangHelper;
public static class StartupService
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "GeosangIntegratedHub";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue(Name) != null; } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled)
        {
            string path = Environment.ProcessPath ?? throw new InvalidOperationException("실행 파일 경로를 찾지 못했습니다.");
            if (!string.Equals(System.IO.Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("배포된 실행 파일로 실행한 뒤 자동 실행을 설정해주세요.");
            key.SetValue(Name, $"\"{path}\"");
        }
        else key.DeleteValue(Name, false);
    }
}
