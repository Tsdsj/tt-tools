using System.Windows.Media;

namespace TtLauncher.Models;

/// <summary>
/// 统一搜索结果模型
/// </summary>
public class SearchResultItem
{
    /// <summary>
    /// 主标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 副标题
    /// </summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>
    /// 右侧标签
    /// </summary>
    public string Tag { get; set; } = "APP";

    /// <summary>
    /// 默认执行路径
    /// </summary>
    public string ExecutePath { get; set; } = string.Empty;

    /// <summary>
    /// 默认执行参数
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// 结果图标
    /// </summary>
    public ImageSource? Icon { get; set; }

    /// <summary>
    /// 搜索匹配得分，越高越靠前
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// 结果类型
    /// </summary>
    public SearchResultKind Kind { get; set; } = SearchResultKind.App;

    /// <summary>
    /// 是否允许执行主操作
    /// </summary>
    public bool CanExecute { get; set; } = true;

    /// <summary>
    /// 是否记录应用使用行为
    /// </summary>
    public bool ShouldTrackUsage { get; set; }

    /// <summary>
    /// 自定义执行逻辑
    /// </summary>
    public Func<CancellationToken, Task<SearchResultActionResult>>? ExecuteAsync { get; set; }

    /// <summary>
    /// 创建提示项
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="subtitle">说明</param>
    /// <param name="tag">标签</param>
    /// <returns>提示结果</returns>
    public static SearchResultItem CreateInfo(string title, string subtitle, string tag = "INFO")
    {
        return new SearchResultItem
        {
            Title = title,
            Subtitle = subtitle,
            Tag = tag,
            Kind = SearchResultKind.Info,
            CanExecute = false
        };
    }
}
