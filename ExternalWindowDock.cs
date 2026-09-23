using System.Diagnostics;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace GeosangHub;

public sealed class ExternalWindowDock : IDisposable
{
    private readonly Forms.Panel _host;
    private readonly Forms.Timer _resizeTimer;
    private IntPtr _window;
    private IntPtr _originalParent;
    private nint _originalStyle;
    private nint _originalExStyle;
    private nint _dockedStyle;
    private nint _dockedExStyle;
    private WINDOWPLACEMENT _originalPlacement;

    public ExternalWindowDock(Forms.Panel host)
    {
        _host = host;
        _host.Resize += (_, _) => FitWindow();
        _resizeTimer = new Forms.Timer { Interval = 500 };
        _resizeTimer.Tick += (_, _) =>
        {
            if (_window != IntPtr.Zero && !Native.IsWindow(_window)) Detach(restoreWindow: false);
            else FitWindow();
        };
    }

    public bool IsAttached => _window != IntPtr.Zero;
    public event EventHandler? Detached;

    public void Attach(IntPtr window)
    {
        if (window == IntPtr.Zero || !Native.IsWindow(window))
            throw new InvalidOperationException("선택한 프로그램 창을 찾을 수 없습니다.");

        Detach();
        _window = window;
        _originalParent = Native.GetParent(window);
        _originalStyle = Native.GetWindowLongPtr(window, Native.GWL_STYLE);
        _originalExStyle = Native.GetWindowLongPtr(window, Native.GWL_EXSTYLE);
        _originalPlacement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        Native.GetWindowPlacement(window, ref _originalPlacement);

        Native.ShowWindow(window, Native.SW_RESTORE);
        _dockedStyle = (_originalStyle | Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_CLIPCHILDREN | Native.WS_CLIPSIBLINGS) &
            ~(Native.WS_POPUP | Native.WS_CAPTION | Native.WS_THICKFRAME | Native.WS_MINIMIZEBOX | Native.WS_MAXIMIZEBOX | Native.WS_SYSMENU);
        _dockedExStyle = _originalExStyle & ~Native.WS_EX_APPWINDOW;
        Native.SetWindowLongPtr(window, Native.GWL_STYLE, _dockedStyle);
        Native.SetWindowLongPtr(window, Native.GWL_EXSTYLE, _dockedExStyle);
        Native.SetLastError(0);
        IntPtr previousParent = Native.SetParent(window, _host.Handle);
        int error = Marshal.GetLastWin32Error();
        if (previousParent == IntPtr.Zero && error != 0)
        {
            RestoreWindowState(window);
            _window = IntPtr.Zero;
            throw new InvalidOperationException($"이 프로그램은 상단 영역에 연결할 수 없습니다. (Windows 오류 {error})");
        }

        _resizeTimer.Start();
        FitWindow(forceFrameChange: true);
        Native.SetForegroundWindow(window);
    }

    public void Detach(bool restoreWindow = true)
    {
        if (_window == IntPtr.Zero) return;
        IntPtr window = _window;
        _window = IntPtr.Zero;
        _resizeTimer.Stop();
        if (restoreWindow && Native.IsWindow(window)) RestoreWindowState(window);
        Detached?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreWindowState(IntPtr window)
    {
        Native.SetParent(window, _originalParent);
        Native.SetWindowLongPtr(window, Native.GWL_STYLE, _originalStyle);
        Native.SetWindowLongPtr(window, Native.GWL_EXSTYLE, _originalExStyle);
        Native.SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW);
        if (_originalPlacement.length != 0) Native.SetWindowPlacement(window, ref _originalPlacement);
    }

    private void FitWindow(bool forceFrameChange = false)
    {
        if (_window == IntPtr.Zero || !Native.IsWindow(_window) || !_host.IsHandleCreated) return;
        bool frameChanged = forceFrameChange;
        if (Native.GetParent(_window) != _host.Handle)
        {
            Native.SetParent(_window, _host.Handle);
            frameChanged = true;
        }
        if (Native.GetWindowLongPtr(_window, Native.GWL_STYLE) != _dockedStyle)
        {
            Native.SetWindowLongPtr(_window, Native.GWL_STYLE, _dockedStyle);
            frameChanged = true;
        }
        if (Native.GetWindowLongPtr(_window, Native.GWL_EXSTYLE) != _dockedExStyle)
        {
            Native.SetWindowLongPtr(_window, Native.GWL_EXSTYLE, _dockedExStyle);
            frameChanged = true;
        }
        if (Native.IsZoomed(_window) || Native.IsIconic(_window)) Native.ShowWindow(_window, Native.SW_RESTORE);
        int width = Math.Max(1, _host.ClientSize.Width);
        int height = Math.Max(1, _host.ClientSize.Height);
        uint flags = Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW;
        if (frameChanged) flags |= Native.SWP_FRAMECHANGED;
        Native.SetWindowPos(_window, IntPtr.Zero, 0, 0, width, height, flags);
    }

    public void Dispose()
    {
        Detach();
        _resizeTimer.Dispose();
    }

    public static IReadOnlyList<ExternalWindowInfo> GetAvailableWindows(IntPtr excludedWindow)
    {
        var result = new List<ExternalWindowInfo>();
        uint ownProcess = (uint)Environment.ProcessId;
        Native.EnumWindows((window, _) =>
        {
            if (window == excludedWindow || !Native.IsWindowVisible(window) || Native.GetWindowTextLength(window) == 0)
                return true;
            Native.GetWindowThreadProcessId(window, out uint processId);
            if (processId == ownProcess) return true;
            nint exStyle = Native.GetWindowLongPtr(window, Native.GWL_EXSTYLE);
            if ((exStyle & Native.WS_EX_TOOLWINDOW) != 0) return true;
            try
            {
                string title = Native.GetWindowTitle(window);
                string processName = Process.GetProcessById((int)processId).ProcessName;
                result.Add(new ExternalWindowInfo(window, title, processName, (int)processId));
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return result.OrderBy(x => x.ProcessName).ThenBy(x => x.Title).ToArray();
    }

    private static class Native
    {
        internal const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        internal static readonly nint WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPCHILDREN = 0x02000000,
            WS_CLIPSIBLINGS = 0x04000000, WS_POPUP = unchecked((nint)0x80000000u), WS_CAPTION = 0x00C00000,
            WS_THICKFRAME = 0x00040000, WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000,
            WS_SYSMENU = 0x00080000, WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;
        internal const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004,
            SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040;
        internal const int SW_RESTORE = 9;

        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr state);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", SetLastError = true)] private static extern nint GetWindowLongPtrW(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLongW(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowLongPtrW(IntPtr window, int index, nint value);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLongW(IntPtr window, int index, int value);
        [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(IntPtr window, ref WINDOWPLACEMENT placement);
        [DllImport("user32.dll")] internal static extern bool SetWindowPlacement(IntPtr window, ref WINDOWPLACEMENT placement);
        [DllImport("kernel32.dll")] internal static extern void SetLastError(uint error);

        internal static nint GetWindowLongPtr(IntPtr window, int index) =>
            IntPtr.Size == 8 ? GetWindowLongPtrW(window, index) : GetWindowLongW(window, index);
        internal static nint SetWindowLongPtr(IntPtr window, int index, nint value) =>
            IntPtr.Size == 8 ? SetWindowLongPtrW(window, index, value) : SetWindowLongW(window, index, (int)value);
        internal static string GetWindowTitle(IntPtr window)
        {
            var text = new System.Text.StringBuilder(GetWindowTextLength(window) + 1);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }
    }
}

public sealed record ExternalWindowInfo(IntPtr Handle, string Title, string ProcessName, int ProcessId)
{
    public string DisplayName => $"{Title}  ·  {ProcessName}";
}

[StructLayout(LayoutKind.Sequential)]
internal struct WINDOWPLACEMENT
{
    public int length;
    public int flags;
    public int showCmd;
    public System.Drawing.Point ptMinPosition;
    public System.Drawing.Point ptMaxPosition;
    public System.Drawing.Rectangle rcNormalPosition;
}
