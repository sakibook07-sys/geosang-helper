using System.Buffers.Binary;
using System.Net;

namespace GeosangHelper;

internal sealed record GameServerMessage(byte Opcode, byte[] Data, DateTimeOffset ReceivedAt);

internal sealed class GameNetworkMonitor : IDisposable
{
    private readonly List<LivePcapDevice> devices = new();
    private readonly Dictionary<string, TcpStreamState> streams = new();
    private readonly object gate = new();
    internal event Action<GameServerMessage>? MessageReceived;
    internal int DeviceCount => devices.Count;

    internal void Start()
    {
        if (devices.Count > 0) return;
        Exception? lastError = null;
        foreach (var device in PcapNative.GetDevices())
        {
            try { devices.Add(LivePcapDevice.Start(device, "tcp and src net 211.233.0.0/16 and src port 8000", AcceptFrame)); }
            catch (Exception ex) { lastError = ex; }
        }
        if (devices.Count == 0) throw new InvalidOperationException("게임 통신을 읽을 장치를 열지 못했습니다. Npcap 설치 또는 관리자 권한을 확인해주세요.", lastError);
    }

    private void AcceptFrame(byte[] frame)
    {
        if (frame.Length < 54 || frame[12] != 0x08 || frame[13] != 0x00) return;
        int ip = 14, ipHeader = (frame[ip] & 0x0F) * 4;
        if (ipHeader < 20 || frame[ip + 9] != 6) return;
        int tcp = ip + ipHeader;
        if (tcp + 20 > frame.Length) return;
        int tcpHeader = (frame[tcp + 12] >> 4) * 4;
        int start = tcp + tcpHeader;
        int total = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(ip + 2));
        int length = Math.Min(total - ipHeader - tcpHeader, frame.Length - start);
        if (length <= 0) return;
        uint sequence = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(tcp + 4));
        string key = $"{new IPAddress(frame.AsSpan(ip + 12, 4))}:{BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(tcp))}>{new IPAddress(frame.AsSpan(ip + 16, 4))}:{BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(tcp + 2))}";
        var payload = frame.AsSpan(start, length).ToArray();
        List<GameServerMessage> messages;
        lock (gate)
        {
            if (!streams.TryGetValue(key, out var stream)) streams[key] = stream = new TcpStreamState();
            messages = stream.Add(sequence, payload, DateTimeOffset.Now);
        }
        foreach (var message in messages) MessageReceived?.Invoke(message);
    }

    public void Dispose()
    {
        foreach (var device in devices) device.Dispose();
        devices.Clear();
        lock (gate) streams.Clear();
    }

    private sealed class TcpStreamState
    {
        private readonly List<byte> buffer = new();
        private uint? nextSequence;

        internal List<GameServerMessage> Add(uint sequence, byte[] payload, DateTimeOffset receivedAt)
        {
            if (nextSequence.HasValue)
            {
                long overlap = (long)nextSequence.Value - sequence;
                if (overlap >= payload.Length) return new();
                if (overlap > 0) payload = payload[(int)overlap..];
                else if (overlap < 0) { buffer.Clear(); }
            }
            nextSequence = sequence + (uint)payload.Length;
            buffer.AddRange(payload);
            var result = new List<GameServerMessage>();
            while (buffer.Count >= 2)
            {
                int length = buffer[0] | buffer[1] << 8;
                if (length < 4 || length > 65535) { buffer.Clear(); break; }
                if (buffer.Count < length) break;
                byte[] data = buffer.GetRange(0, length).ToArray();
                buffer.RemoveRange(0, length);
                if (data[2] == 0) result.Add(new(data[3], data, receivedAt));
            }
            return result;
        }
    }
}
