using TtLauncher.Models;

namespace TtLauncher.Commands;

/// <summary>
/// 查询解析器 — 将用户输入拆分为命令前缀 + 参数
/// </summary>
public static class QueryParser
{
    // 已注册的命令前缀
    private static readonly HashSet<string> KnownPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "f",      // Everything 文件搜索
        "ocr",    // OCR
        "port",   // 端口查询
        "ports",  // 端口列表
    };

    public static ParsedQuery Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParsedQuery { RawInput = input ?? string.Empty };
        }

        var trimmed = input.Trim();
        var spaceIndex = trimmed.IndexOf(' ');

        if (spaceIndex > 0)
        {
            var prefix = trimmed[..spaceIndex];
            if (KnownPrefixes.Contains(prefix))
            {
                return new ParsedQuery
                {
                    RawInput = trimmed,
                    CommandPrefix = prefix.ToLowerInvariant(),
                    Argument = trimmed[(spaceIndex + 1)..].Trim()
                };
            }
        }
        else
        {
            // 无参数的命令（如 "ocr", "ports"）
            if (KnownPrefixes.Contains(trimmed))
            {
                return new ParsedQuery
                {
                    RawInput = trimmed,
                    CommandPrefix = trimmed.ToLowerInvariant(),
                    Argument = string.Empty
                };
            }
        }

        // 普通搜索
        return new ParsedQuery
        {
            RawInput = trimmed,
            CommandPrefix = null,
            Argument = trimmed
        };
    }

    /// <summary>
    /// 注册新命令前缀（供扩展使用）
    /// </summary>
    public static void RegisterPrefix(string prefix)
    {
        KnownPrefixes.Add(prefix.ToLowerInvariant());
    }
}
