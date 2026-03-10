using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TtLauncher.Commands;
using TtLauncher.Models;
using TtLauncher.Services;

namespace TtLauncher.ViewModels;

/// <summary>
/// 主窗口视图模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int SearchDebounceDelayMs = 80;
    private readonly CommandRouter _router;
    private readonly AppIndexService _indexService;
    private CancellationTokenSource? _searchCts;

    public MainViewModel(CommandRouter router, AppIndexService indexService)
    {
        _router = router;
        _indexService = indexService;
    }

    /// <summary>
    /// 当前查询文本
    /// </summary>
    [ObservableProperty]
    private string _queryText = string.Empty;

    /// <summary>
    /// 当前选中索引
    /// </summary>
    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>
    /// 是否处于加载中
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 当前是否存在结果
    /// </summary>
    [ObservableProperty]
    private bool _hasResults;

    /// <summary>
    /// 状态提示
    /// </summary>
    [ObservableProperty]
    private string _statusText = "输入关键词搜索应用...";

    public ObservableCollection<SearchResultItem> Results { get; } = new();

    partial void OnQueryTextChanged(string value)
    {
        _ = PerformSearchAsync(value);
    }

    /// <summary>
    /// 初始化应用索引
    /// </summary>
    /// <returns>任务</returns>
    public async Task InitializeAsync()
    {
        StatusText = "正在扫描本地应用...";
        IsLoading = true;

        await _indexService.RebuildIndexAsync();

        StatusText = "输入关键词搜索应用...";
        IsLoading = false;
    }

    /// <summary>
    /// 重置查询状态
    /// </summary>
    public void Reset()
    {
        QueryText = string.Empty;
        ClearResults("输入关键词搜索应用...");
    }

    public event Action? RequestHide;

    [RelayCommand]
    private async Task ExecuteSelectedAsync()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Results.Count)
        {
            return;
        }

        var item = Results[SelectedIndex];
        if (!item.CanExecute)
        {
            StatusText = item.Subtitle;
            return;
        }

        try
        {
            SearchResultActionResult actionResult;
            if (item.ExecuteAsync is not null)
            {
                actionResult = await item.ExecuteAsync(CancellationToken.None);
            }
            else if (!string.IsNullOrWhiteSpace(item.ExecutePath))
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = item.ExecutePath,
                    Arguments = item.Arguments,
                    UseShellExecute = true
                };

                Process.Start(processStartInfo);
                actionResult = SearchResultActionResult.Hide();
            }
            else
            {
                StatusText = "当前结果不支持执行";
                return;
            }

            if (!string.IsNullOrWhiteSpace(actionResult.StatusMessage))
            {
                StatusText = actionResult.StatusMessage;
            }

            if (item.ShouldTrackUsage)
            {
                _indexService.RecordUsage(item);
            }

            if (actionResult.ShouldHideWindow)
            {
                RequestHide?.Invoke();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"执行失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void MoveSelectionUp()
    {
        if (Results.Count == 0)
        {
            return;
        }

        SelectedIndex = SelectedIndex <= 0 ? Results.Count - 1 : SelectedIndex - 1;
    }

    [RelayCommand]
    private void MoveSelectionDown()
    {
        if (Results.Count == 0)
        {
            return;
        }

        SelectedIndex = SelectedIndex >= Results.Count - 1 ? 0 : SelectedIndex + 1;
    }

    private async Task PerformSearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ClearResults("输入关键词搜索应用...");
                return;
            }

            IsLoading = true;
            await Task.Delay(SearchDebounceDelayMs, ct);

            var parsedQuery = QueryParser.Parse(query);
            if (parsedQuery.IsCommand && !_router.HasProvider(parsedQuery.CommandPrefix!))
            {
                SetResults(
                [
                    SearchResultItem.CreateInfo(
                        $"命令 {parsedQuery.CommandPrefix} 尚未实现",
                        "请确认命令前缀是否正确。")
                ],
                $"命令 {parsedQuery.CommandPrefix} 尚未实现");
                return;
            }

            var results = await _router.RouteAsync(parsedQuery, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (results.Count == 0)
            {
                ClearResults("未找到匹配结果");
                return;
            }

            SetResults(results, $"已找到 {results.Count} 条结果");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetResults(
            [
                SearchResultItem.CreateInfo("搜索失败", ex.Message, "ERROR")
            ],
            $"搜索失败：{ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetResults(IReadOnlyList<SearchResultItem> items, string statusText)
    {
        Results.Clear();
        foreach (var item in items)
        {
            Results.Add(item);
        }

        HasResults = Results.Count > 0;
        SelectedIndex = Results
            .Select((item, index) => new { item, index })
            .Where(x => x.item.CanExecute)
            .Select(x => x.index)
            .DefaultIfEmpty(-1)
            .First();
        StatusText = statusText;
    }

    private void ClearResults(string statusText)
    {
        Results.Clear();
        HasResults = false;
        SelectedIndex = -1;
        StatusText = statusText;
    }
}
