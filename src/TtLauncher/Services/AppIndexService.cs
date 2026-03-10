using TtLauncher.Models;
using TtLauncher.Providers;

namespace TtLauncher.Services;

/// <summary>
/// 应用索引服务 — 管理应用扫描和索引刷新
/// </summary>
public class AppIndexService
{
    private readonly AppSearchProvider _provider;

    public AppIndexService(AppSearchProvider provider)
    {
        _provider = provider;
    }

    public async Task RebuildIndexAsync()
    {
        await _provider.InitializeAsync();
    }

    /// <summary>
    /// 记录使用（为最近使用功能预留）
    /// </summary>
    public void RecordUsage(SearchResultItem item)
    {
        // TODO: 记录使用频率和时间，用于后续排序优化
    }
}
