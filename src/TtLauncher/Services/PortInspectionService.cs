using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace TtLauncher.Services;

/// <summary>
/// 端口占用检测服务
/// </summary>
public class PortInspectionService
{
    private const int AddressFamilyInterNetwork = 2;
    private const int AddressFamilyInterNetworkV6 = 23;
    private const uint ErrorSuccess = 0;
    private const uint ErrorInsufficientBuffer = 122;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<int, string> _processNameCache = new();
    private IReadOnlyList<PortInspectionEntry> _cachedEntries = Array.Empty<PortInspectionEntry>();
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;

    /// <summary>
    /// 查询指定端口
    /// </summary>
    /// <param name="port">端口号</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>查询结果</returns>
    public async Task<IReadOnlyList<PortInspectionEntry>> QueryPortAsync(int port, CancellationToken ct = default)
    {
        var entries = await GetListeningPortsAsync(ct);
        return entries.Where(item => item.Port == port).ToList();
    }

    /// <summary>
    /// 获取当前监听端口
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>监听端口列表</returns>
    public async Task<IReadOnlyList<PortInspectionEntry>> GetListeningPortsAsync(CancellationToken ct = default)
    {
        if (_cacheExpiresAt > DateTimeOffset.UtcNow && _cachedEntries.Count > 0)
        {
            return _cachedEntries;
        }

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_cacheExpiresAt > DateTimeOffset.UtcNow && _cachedEntries.Count > 0)
            {
                return _cachedEntries;
            }

            var entries = await Task.Run(ReadListeningPorts, ct);
            _cachedEntries = entries;
            _cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheDuration);
            return _cachedEntries;
        }
        catch (Exception ex)
        {
            return
            [
                new PortInspectionEntry
                {
                    ProcessName = "端口检测不可用",
                    Protocol = "INFO",
                    LocalAddress = ex.Message,
                    Port = 0
                }
            ];
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private IReadOnlyList<PortInspectionEntry> ReadListeningPorts()
    {
        var tcpRows = ReadTcpRows(AddressFamilyInterNetwork)
            .Concat(ReadTcpRows(AddressFamilyInterNetworkV6));
        var udpRows = ReadUdpRows(AddressFamilyInterNetwork)
            .Concat(ReadUdpRows(AddressFamilyInterNetworkV6));

        return tcpRows
            .Concat(udpRows)
            .OrderBy(item => item.Port)
            .ThenBy(item => item.Protocol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<PortInspectionEntry> ReadTcpRows(int addressFamily)
    {
        foreach (var row in GetTcpOwnerRows(addressFamily))
        {
            yield return new PortInspectionEntry
            {
                Protocol = "TCP",
                LocalAddress = row.Address,
                Port = row.Port,
                Pid = row.Pid,
                ProcessName = ResolveProcessName(row.Pid)
            };
        }
    }

    private IEnumerable<PortInspectionEntry> ReadUdpRows(int addressFamily)
    {
        foreach (var row in GetUdpOwnerRows(addressFamily))
        {
            yield return new PortInspectionEntry
            {
                Protocol = "UDP",
                LocalAddress = row.Address,
                Port = row.Port,
                Pid = row.Pid,
                ProcessName = ResolveProcessName(row.Pid)
            };
        }
    }

    private IEnumerable<PortOwnerRow> GetTcpOwnerRows(int addressFamily)
    {
        var bufferSize = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, addressFamily, TcpTableClass.TcpTableOwnerPidListeners, 0);
        return ReadTable(
            bufferSize,
            (IntPtr buffer, ref int size) => GetExtendedTcpTable(buffer, ref size, true, addressFamily, TcpTableClass.TcpTableOwnerPidListeners, 0),
            addressFamily,
            isTcp: true);
    }

    private IEnumerable<PortOwnerRow> GetUdpOwnerRows(int addressFamily)
    {
        var bufferSize = 0;
        _ = GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, addressFamily, UdpTableClass.UdpTableOwnerPid, 0);
        return ReadTable(
            bufferSize,
            (IntPtr buffer, ref int size) => GetExtendedUdpTable(buffer, ref size, true, addressFamily, UdpTableClass.UdpTableOwnerPid, 0),
            addressFamily,
            isTcp: false);
    }

    private IEnumerable<PortOwnerRow> ReadTable(
        int initialBufferSize,
        TableReader tableReader,
        int addressFamily,
        bool isTcp)
    {
        var bufferSize = initialBufferSize;
        if (bufferSize <= 0)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            var result = tableReader(buffer, ref bufferSize);
            if (result != ErrorSuccess)
            {
                yield break;
            }

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));

            if (isTcp)
            {
                if (addressFamily == AddressFamilyInterNetwork)
                {
                    var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
                    for (var index = 0; index < rowCount; index++)
                    {
                        var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                        yield return new PortOwnerRow(ToIpv4Address(row.localAddr), ConvertPort(row.localPort), unchecked((int)row.owningPid));
                        rowPointer = IntPtr.Add(rowPointer, rowSize);
                    }

                    yield break;
                }

                var ipv6RowSize = Marshal.SizeOf<MibTcp6RowOwnerPid>();
                for (var index = 0; index < rowCount; index++)
                {
                    var row = Marshal.PtrToStructure<MibTcp6RowOwnerPid>(rowPointer);
                    yield return new PortOwnerRow(ToIpv6Address(row.localAddr), ConvertPort(row.localPort), unchecked((int)row.owningPid));
                    rowPointer = IntPtr.Add(rowPointer, ipv6RowSize);
                }

                yield break;
            }

            if (addressFamily == AddressFamilyInterNetwork)
            {
                var rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();
                for (var index = 0; index < rowCount; index++)
                {
                    var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPointer);
                    yield return new PortOwnerRow(ToIpv4Address(row.localAddr), ConvertPort(row.localPort), unchecked((int)row.owningPid));
                    rowPointer = IntPtr.Add(rowPointer, rowSize);
                }

                yield break;
            }

            var udpIpv6RowSize = Marshal.SizeOf<MibUdp6RowOwnerPid>();
            for (var index = 0; index < rowCount; index++)
            {
                var row = Marshal.PtrToStructure<MibUdp6RowOwnerPid>(rowPointer);
                yield return new PortOwnerRow(ToIpv6Address(row.localAddr), ConvertPort(row.localPort), unchecked((int)row.owningPid));
                rowPointer = IntPtr.Add(rowPointer, udpIpv6RowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private string ResolveProcessName(int pid)
    {
        lock (_processNameCache)
        {
            if (_processNameCache.TryGetValue(pid, out var cachedName))
            {
                return cachedName;
            }
        }

        string processName;
        try
        {
            using var process = Process.GetProcessById(pid);
            processName = process.ProcessName;
        }
        catch
        {
            processName = $"PID {pid}";
        }

        lock (_processNameCache)
        {
            _processNameCache[pid] = processName;
        }

        return processName;
    }

    private static string ToIpv4Address(uint address)
    {
        return new IPAddress(address).ToString();
    }

    private static string ToIpv6Address(byte[] addressBytes)
    {
        return new IPAddress(addressBytes).ToString();
    }

    private static int ConvertPort(byte[] portBytes)
    {
        return (portBytes[0] << 8) + portBytes[1];
    }

    private delegate uint TableReader(IntPtr buffer, ref int size);

    private readonly record struct PortOwnerRow(string Address, int Port, int Pid);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool sort,
        int ipVersion,
        TcpTableClass tableClass,
        uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr udpTable,
        ref int size,
        bool sort,
        int ipVersion,
        UdpTableClass tableClass,
        uint reserved);

    private enum TcpTableClass
    {
        TcpTableOwnerPidListeners = 3
    }

    private enum UdpTableClass
    {
        UdpTableOwnerPid = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint state;
        public uint localAddr;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] localPort;

        public uint remoteAddr;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] remotePort;

        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] localAddr;

        public uint localScopeId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] localPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] remoteAddr;

        public uint remoteScopeId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] remotePort;

        public uint state;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdpRowOwnerPid
    {
        public uint localAddr;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] localPort;

        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] localAddr;

        public uint localScopeId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] localPort;

        public uint owningPid;
    }
}

/// <summary>
/// 端口检测条目
/// </summary>
public sealed class PortInspectionEntry
{
    /// <summary>
    /// 协议
    /// </summary>
    public string Protocol { get; init; } = string.Empty;

    /// <summary>
    /// 本地地址
    /// </summary>
    public string LocalAddress { get; init; } = string.Empty;

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// 进程 ID
    /// </summary>
    public int Pid { get; init; }

    /// <summary>
    /// 进程名
    /// </summary>
    public string ProcessName { get; init; } = string.Empty;
}
