using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GeosangHelper;

internal static class PcapNative
{
    private const string Library = "wpcap.dll";
    private const int ErrorBufferSize = 256;

    static PcapNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(PcapNative).Assembly, (name, _, _) =>
        {
            if (!name.Equals(Library, StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            foreach (string path in new[] { Path.Combine(windows, "System32", "Npcap", Library), Path.Combine(windows, "System32", Library) })
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out IntPtr handle)) return handle;
            return IntPtr.Zero;
        });
    }

    [StructLayout(LayoutKind.Sequential)] private struct PcapIf { public IntPtr Next, Name, Description, Addresses; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct BpfProgram { public uint Length; public IntPtr Instructions; }
    [StructLayout(LayoutKind.Sequential)] internal struct PcapHeader { public uint Seconds, Microseconds, CapturedLength, OriginalLength; }
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern int pcap_findalldevs(out IntPtr devices, StringBuilder error);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void pcap_freealldevs(IntPtr devices);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] internal static extern IntPtr pcap_open_live(string device, int snapshotLength, int promiscuous, int timeoutMs, StringBuilder error);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern int pcap_compile(IntPtr handle, out BpfProgram program, string filter, int optimize, uint netmask);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int pcap_setfilter(IntPtr handle, ref BpfProgram program);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void pcap_freecode(ref BpfProgram program);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int pcap_next_ex(IntPtr handle, out IntPtr header, out IntPtr data);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void pcap_breakloop(IntPtr handle);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void pcap_close(IntPtr handle);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr pcap_geterr(IntPtr handle);

    internal sealed record DeviceInfo(string Name, string Description);

    internal static IReadOnlyList<DeviceInfo> GetDevices()
    {
        var error = new StringBuilder(ErrorBufferSize);
        if (pcap_findalldevs(out IntPtr first, error) != 0) throw new InvalidOperationException("Npcap 장치 검색 실패: " + error);
        var result = new List<DeviceInfo>();
        try
        {
            for (IntPtr current = first; current != IntPtr.Zero;)
            {
                var item = Marshal.PtrToStructure<PcapIf>(current);
                string? name = Marshal.PtrToStringAnsi(item.Name);
                string? description = Marshal.PtrToStringAnsi(item.Description);
                if (!string.IsNullOrWhiteSpace(name)) result.Add(new(name, description ?? name));
                current = item.Next;
            }
        }
        finally { if (first != IntPtr.Zero) pcap_freealldevs(first); }
        return result;
    }

    internal static void SetFilter(IntPtr handle, string filter)
    {
        if (pcap_compile(handle, out BpfProgram program, filter, 1, uint.MaxValue) != 0) throw new InvalidOperationException("캡처 필터 생성 실패: " + GetError(handle));
        try { if (pcap_setfilter(handle, ref program) != 0) throw new InvalidOperationException("캡처 필터 적용 실패: " + GetError(handle)); }
        finally { pcap_freecode(ref program); }
    }

    internal static string GetError(IntPtr handle) => Marshal.PtrToStringAnsi(pcap_geterr(handle)) ?? "알 수 없는 오류";
    internal static StringBuilder ErrorBuffer() => new(ErrorBufferSize);
}

internal sealed class LivePcapDevice : IDisposable
{
    private IntPtr handle;
    private readonly CancellationTokenSource cancellation = new();
    private Task? task;
    private readonly Action<byte[]> onPacket;

    private LivePcapDevice(Action<byte[]> onPacket) => this.onPacket = onPacket;

    internal static LivePcapDevice Start(PcapNative.DeviceInfo device, string filter, Action<byte[]> onPacket)
    {
        var capture = new LivePcapDevice(onPacket);
        try
        {
            var error = PcapNative.ErrorBuffer();
            capture.handle = PcapNative.pcap_open_live(device.Name, 65535, 0, 500, error);
            if (capture.handle == IntPtr.Zero) throw new InvalidOperationException(error.ToString());
            PcapNative.SetFilter(capture.handle, filter);
            capture.task = Task.Run(capture.Loop);
            return capture;
        }
        catch { capture.Dispose(); throw; }
    }

    private void Loop()
    {
        while (!cancellation.IsCancellationRequested)
        {
            int result = PcapNative.pcap_next_ex(handle, out IntPtr headerPointer, out IntPtr dataPointer);
            if (result == 0) continue;
            if (result < 0) break;
            var header = Marshal.PtrToStructure<PcapNative.PcapHeader>(headerPointer);
            if (header.CapturedLength is 0 or > 65535) continue;
            var frame = new byte[header.CapturedLength];
            Marshal.Copy(dataPointer, frame, 0, frame.Length);
            onPacket(frame);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        if (handle != IntPtr.Zero) PcapNative.pcap_breakloop(handle);
        try { task?.Wait(1500); } catch { }
        if (handle != IntPtr.Zero) { PcapNative.pcap_close(handle); handle = IntPtr.Zero; }
        cancellation.Dispose();
    }
}
