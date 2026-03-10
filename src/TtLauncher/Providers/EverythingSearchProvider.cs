using TtLauncher.Models;
using TtLauncher.Services;

namespace TtLauncher.Providers;

/// <summary>
/// Everything 文件搜索 Provider
/// </summary>
public class EverythingSearchProvider : ISearchProvider
{
    private readonly EverythingSearchService _searchService;

    public EverythingSearchProvider(EverythingSearchService searchService)
    {
        _searchService = searchService;
    }

    public string Name => "Everything 文件搜索";

    public string? CommandPrefix => "f";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        var response = await _searchService.SearchAsync(query, 10, ct);
        if (!response.IsSuccess)
        {
            return
            [
                SearchResultItem.CreateInfo(response.MessageTitle, response.MessageSubtitle, "FILE")
            ];
        }

        if (response.Entries.Count == 0)
        {
            return
            [
                SearchResultItem.CreateInfo("Everything 未命中文件", $"关键词：{query}", "FILE")
            ];
        }

        return response.Entries
            .Select((entry, index) => new SearchResultItem
            {
                Title = entry.Name,
                Subtitle = entry.ParentDirectory,
                Tag = entry.IsDirectory ? "FOLDER" : "FILE",
                ExecutePath = entry.FullPath,
                Score = 100 - index,
                Kind = entry.IsDirectory ? SearchResultKind.Folder : SearchResultKind.File
            })
            .ToList();
    }
}
