using TtLauncher.Models;
using TtLauncher.Providers;

namespace TtLauncher.Commands;

/// <summary>
/// 命令路由器 — 根据解析结果分发到对应的 SearchProvider
/// </summary>
public class CommandRouter
{
    private readonly Dictionary<string, ISearchProvider> _commandProviders = new(StringComparer.OrdinalIgnoreCase);
    private ISearchProvider? _defaultProvider;

    /// <summary>
    /// 注册默认搜索提供者（无命令前缀时使用）
    /// </summary>
    public void RegisterDefault(ISearchProvider provider)
    {
        _defaultProvider = provider;
    }

    /// <summary>
    /// 注册命令提供者
    /// </summary>
    public void Register(string commandPrefix, ISearchProvider provider)
    {
        _commandProviders[commandPrefix] = provider;
    }

    /// <summary>
    /// 路由查询到对应提供者
    /// </summary>
    public async Task<IReadOnlyList<SearchResultItem>> RouteAsync(ParsedQuery query, CancellationToken ct = default)
    {
        if (query.IsCommand && _commandProviders.TryGetValue(query.CommandPrefix!, out var provider))
        {
            return await provider.SearchAsync(query.Argument, ct);
        }

        if (_defaultProvider != null && !string.IsNullOrWhiteSpace(query.Argument))
        {
            return await _defaultProvider.SearchAsync(query.Argument, ct);
        }

        return Array.Empty<SearchResultItem>();
    }

    /// <summary>
    /// 检查某个命令前缀是否已注册提供者
    /// </summary>
    public bool HasProvider(string commandPrefix)
    {
        return _commandProviders.ContainsKey(commandPrefix);
    }
}
