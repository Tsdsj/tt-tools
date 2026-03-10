using System.Windows.Media;

namespace TtLauncher.Models;

/// <summary>
/// 统一搜索结果模型，所有 Provider 返回此类型
/// </summary>
public class SearchResultItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Tag { get; set; } = "APP";
    public string ExecutePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }

    /// <summary>
    /// 搜索匹配得分，用于排序（越高越靠前）
    /// </summary>
    public int Score { get; set; }
}
