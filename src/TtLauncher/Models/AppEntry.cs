namespace TtLauncher.Models;

/// <summary>
/// 本地应用索引条目
/// </summary>
public class AppEntry
{
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;

    /// <summary>
    /// 最后使用时间（为"最近使用"排序预留）
    /// </summary>
    public DateTime? LastUsedTime { get; set; }

    /// <summary>
    /// 使用次数（为频率排序预留）
    /// </summary>
    public int UseCount { get; set; }
}
