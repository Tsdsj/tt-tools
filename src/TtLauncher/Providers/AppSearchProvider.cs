using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TtLauncher.Infrastructure;
using TtLauncher.Models;

namespace TtLauncher.Providers;

/// <summary>
/// 本地应用搜索 Provider
/// </summary>
public class AppSearchProvider : ISearchProvider
{
    private const int MaxIconCacheEntries = 256;
    private readonly object _lock = new();
    private readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private List<AppEntry> _appIndex = new();
    private bool _indexed;

    public string Name => "应用搜索";

    public string? CommandPrefix => null;

    /// <summary>
    /// 初始化应用索引
    /// </summary>
    /// <returns>任务</returns>
    public async Task InitializeAsync()
    {
        await Task.Run(BuildIndex);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchResultItem>();
        }

        var lowerQuery = query.Trim().ToLowerInvariant();

        var results = await Task.Run(() =>
        {
            return _appIndex
                .Select(app =>
                {
                    var lowerName = app.Name.ToLowerInvariant();
                    var score = CalculateScore(lowerQuery, lowerName, app.ExecutablePath);

                    return new { App = app, Score = score };
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.App.Name.Length)
                .Take(12)
                .Select(item => new SearchResultItem
                {
                    Title = item.App.Name,
                    Subtitle = TruncatePath(item.App.ExecutablePath, 80),
                    Tag = "APP",
                    ExecutePath = item.App.ExecutablePath,
                    Arguments = item.App.Arguments,
                    Icon = GetCachedIcon(item.App.ExecutablePath),
                    Score = item.Score,
                    Kind = SearchResultKind.App,
                    ShouldTrackUsage = true
                })
                .ToList();
        }, ct);

        return results;
    }

    private void BuildIndex()
    {
        lock (_lock)
        {
            if (_indexed)
            {
                return;
            }

            var entries = new List<AppEntry>();
            var candidateDirectories = GetCandidateDirectories();

            foreach (var directory in candidateDirectories)
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    continue;
                }

                try
                {
                    foreach (var shortcutPath in Directory.EnumerateFiles(directory, "*.lnk", SearchOption.AllDirectories))
                    {
                        var entry = ResolveShortcut(shortcutPath);
                        if (entry is not null)
                        {
                            entries.Add(entry);
                        }
                    }
                }
                catch
                {
                    // 忽略无法访问的目录
                }
            }

            _appIndex = entries
                .GroupBy(item => item.ExecutablePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            _indexed = true;
        }
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        }
        .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static int CalculateScore(string query, string lowerName, string executablePath)
    {
        if (lowerName == query)
        {
            return 100;
        }

        if (lowerName.StartsWith(query, StringComparison.Ordinal))
        {
            return 80;
        }

        if (lowerName.Contains(query, StringComparison.Ordinal))
        {
            return 60;
        }

        if (executablePath.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        return 0;
    }

    private static AppEntry? ResolveShortcut(string shortcutPath)
    {
        try
        {
            var resolved = ShortcutResolver.Resolve(shortcutPath);
            if (resolved is null)
            {
                return null;
            }

            var (targetPath, arguments) = resolved.Value;
            if (string.IsNullOrWhiteSpace(targetPath) || !targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var shortcutName = Path.GetFileNameWithoutExtension(shortcutPath);
            if (shortcutName.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                shortcutName.Contains("卸载", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new AppEntry
            {
                Name = shortcutName,
                ExecutablePath = targetPath,
                Arguments = arguments,
                IconPath = targetPath
            };
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? GetCachedIcon(string executablePath)
    {
        lock (_lock)
        {
            if (_iconCache.TryGetValue(executablePath, out var cachedIcon))
            {
                return cachedIcon;
            }
        }

        var icon = ExtractIcon(executablePath);

        lock (_lock)
        {
            if (_iconCache.Count >= MaxIconCacheEntries)
            {
                _iconCache.Clear();
            }

            _iconCache[executablePath] = icon;
        }

        return icon;
    }

    private static ImageSource? ExtractIcon(string executablePath)
    {
        try
        {
            if (!File.Exists(executablePath))
            {
                return null;
            }

            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null)
            {
                return null;
            }

            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            return null;
        }
    }

    private static string TruncatePath(string path, int maxLength)
    {
        if (path.Length <= maxLength)
        {
            return path;
        }

        return "..." + path[^Math.Max(1, maxLength - 3)..];
    }
}
