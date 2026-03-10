namespace TtLauncher.Models;

/// <summary>
/// 解析后的查询对象
/// </summary>
public class ParsedQuery
{
    /// <summary>
    /// 命令前缀，如 "f", "ocr", "port", "ports"。null 表示普通搜索
    /// </summary>
    public string? CommandPrefix { get; set; }

    /// <summary>
    /// 命令参数（前缀后面的部分）
    /// </summary>
    public string Argument { get; set; } = string.Empty;

    /// <summary>
    /// 原始输入
    /// </summary>
    public string RawInput { get; set; } = string.Empty;

    public bool IsCommand => CommandPrefix != null;
}
