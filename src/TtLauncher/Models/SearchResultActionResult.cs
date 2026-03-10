namespace TtLauncher.Models;

/// <summary>
/// 搜索结果执行反馈
/// </summary>
public sealed class SearchResultActionResult
{
    public static readonly SearchResultActionResult None = new();

    /// <summary>
    /// 是否在执行后关闭主窗口
    /// </summary>
    public bool ShouldHideWindow { get; init; }

    /// <summary>
    /// 执行后的状态提示
    /// </summary>
    public string? StatusMessage { get; init; }

    /// <summary>
    /// 生成关闭窗口的反馈
    /// </summary>
    /// <param name="statusMessage">状态提示</param>
    /// <returns>反馈结果</returns>
    public static SearchResultActionResult Hide(string? statusMessage = null)
    {
        return new SearchResultActionResult
        {
            ShouldHideWindow = true,
            StatusMessage = statusMessage
        };
    }

    /// <summary>
    /// 生成状态提示反馈
    /// </summary>
    /// <param name="statusMessage">状态提示</param>
    /// <returns>反馈结果</returns>
    public static SearchResultActionResult Status(string statusMessage)
    {
        return new SearchResultActionResult
        {
            StatusMessage = statusMessage
        };
    }
}
