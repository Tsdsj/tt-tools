using TtLauncher.Models;
using TtLauncher.Services;

namespace TtLauncher.Providers;

/// <summary>
/// 端口检测 Provider
/// </summary>
public class PortSearchProvider : ISearchProvider
{
    private readonly PortInspectionService _inspectionService;
    private readonly bool _listAllPorts;

    public PortSearchProvider(PortInspectionService inspectionService, bool listAllPorts)
    {
        _inspectionService = inspectionService;
        _listAllPorts = listAllPorts;
    }

    public string Name => _listAllPorts ? "监听端口列表" : "端口占用查询";

    public string? CommandPrefix => _listAllPorts ? "ports" : "port";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        IReadOnlyList<PortInspectionEntry> entries;
        if (_listAllPorts)
        {
            entries = await _inspectionService.GetListeningPortsAsync(ct);
        }
        else
        {
            if (!int.TryParse(query, out var targetPort) || targetPort is < 1 or > 65535)
            {
                return
                [
                    SearchResultItem.CreateInfo("端口命令格式不正确", "请使用 port 3000 这种格式查询指定端口。", "PORT")
                ];
            }

            entries = await _inspectionService.QueryPortAsync(targetPort, ct);
            if (entries.Count == 0)
            {
                return
                [
                    SearchResultItem.CreateInfo($"端口 {targetPort} 当前未被监听", "没有检测到 TCP/UDP 监听进程。", "PORT")
                ];
            }
        }

        if (entries.Count == 1 && entries[0].Protocol == "INFO")
        {
            return
            [
                SearchResultItem.CreateInfo(entries[0].ProcessName, entries[0].LocalAddress, "PORT")
            ];
        }

        return entries
            .Take(_listAllPorts ? 80 : 20)
            .Select(CreatePortResult)
            .ToList();
    }

    private static SearchResultItem CreatePortResult(PortInspectionEntry entry)
    {
        var copyText = $"{entry.Protocol} {entry.LocalAddress}:{entry.Port} PID={entry.Pid} {entry.ProcessName}";

        return new SearchResultItem
        {
            Title = entry.ProcessName,
            Subtitle = $"{entry.Protocol}  {entry.LocalAddress}:{entry.Port}  ·  PID {entry.Pid}",
            Tag = entry.Protocol,
            Score = 90,
            Kind = SearchResultKind.Port,
            ExecuteAsync = _ =>
            {
                System.Windows.Clipboard.SetText(copyText);
                return Task.FromResult(SearchResultActionResult.Status("端口信息已复制到剪贴板"));
            }
        };
    }
}
