using TtLauncher.Models;

namespace TtLauncher.Providers;

/// <summary>
/// 搜索提供者接口 — 所有搜索源（应用、文件、端口等）实现此接口
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 该提供者处理的命令前缀。null 表示处理普通搜索（无前缀）
    /// </summary>
    string? CommandPrefix { get; }

    /// <summary>
    /// 搜索
    /// </summary>
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken ct = default);
}
