using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TtLauncher.Commands;
using TtLauncher.Models;
using TtLauncher.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace TtLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly CommandRouter _router;
    private readonly AppIndexService _indexService;
    private CancellationTokenSource? _searchCts;

    public MainViewModel(CommandRouter router, AppIndexService indexService)
    {
        _router = router;
        _indexService = indexService;
    }

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "输入关键词搜索应用...";

    public ObservableCollection<SearchResultItem> Results { get; } = new();

    partial void OnQueryTextChanged(string value)
    {
        _ = PerformSearchAsync(value);
    }

    private async Task PerformSearchAsync(string query)
    {
        // 取消上一次搜索
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Results.Clear();
                SelectedIndex = -1;
                StatusText = "输入关键词搜索应用...";
                return;
            }

            IsLoading = true;

            // 解析查询
            var parsed = QueryParser.Parse(query);

            // 如果是未实现的命令，给提示
            if (parsed.IsCommand && !_router.HasProvider(parsed.CommandPrefix!))
            {
                Results.Clear();
                StatusText = $"命令 \"{parsed.CommandPrefix}\" 尚未实现";
                IsLoading = false;
                return;
            }

            var results = await _router.RouteAsync(parsed, ct);

            if (ct.IsCancellationRequested) return;

            Results.Clear();
            foreach (var item in results)
            {
                Results.Add(item);
            }

            SelectedIndex = Results.Count > 0 ? 0 : -1;
            StatusText = Results.Count > 0 ? $"找到 {Results.Count} 个结果" : "无匹配结果";
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            StatusText = $"搜索出错: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ExecuteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Results.Count) return;

        var item = Results[SelectedIndex];
        if (string.IsNullOrEmpty(item.ExecutePath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = item.ExecutePath,
                Arguments = item.Arguments,
                UseShellExecute = true
            };
            Process.Start(psi);

            // 记录使用
            _indexService.RecordUsage(item);

            // 执行后隐藏窗口
            RequestHide?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void MoveSelectionUp()
    {
        if (Results.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Results.Count - 1 : SelectedIndex - 1;
    }

    [RelayCommand]
    private void MoveSelectionDown()
    {
        if (Results.Count == 0) return;
        SelectedIndex = SelectedIndex >= Results.Count - 1 ? 0 : SelectedIndex + 1;
    }

    public void Reset()
    {
        QueryText = string.Empty;
        Results.Clear();
        SelectedIndex = -1;
        StatusText = "输入关键词搜索应用...";
    }

    /// <summary>
    /// 请求隐藏窗口（由 View 订阅）
    /// </summary>
    public event Action? RequestHide;

    public async Task InitializeAsync()
    {
        StatusText = "正在扫描本地应用...";
        IsLoading = true;
        await _indexService.RebuildIndexAsync();
        StatusText = "输入关键词搜索应用...";
        IsLoading = false;
    }
}
