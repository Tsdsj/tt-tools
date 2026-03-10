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
/// 本地应用搜索提供者 — 扫描开始菜单、桌面快捷方式
/// </summary>
public class AppSearchProvider : ISearchProvider
{
    public string Name => "应用搜索";
    public string? CommandPrefix => null; // 普通搜索

    private List<AppEntry> _appIndex = new();
    private bool _indexed;
    private readonly object _lock = new();
    private readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        await Task.Run(BuildIndex);
    }

    private void BuildIndex()
    {
        lock (_lock)
        {
            if (_indexed) return;

            var entries = new List<AppEntry>();

            // 扫描开始菜单
            var startMenuPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs"),
                @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs"
            };

            // 扫描桌面
            var desktopPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            var allPaths = startMenuPaths.Concat(desktopPaths).Distinct();

            foreach (var basePath in allPaths)
            {
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                    continue;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(basePath, "*.lnk", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var entry = ResolveShortcut(file);
                            if (entry != null)
                                entries.Add(entry);
                        }
                        catch
                        {
                            // 跳过无法解析的快捷方式
                        }
                    }
                }
                catch
                {
                    // 跳过无权限的目录
                }
            }

            // 去重（按可执行路径）
            _appIndex = entries
                .GroupBy(e => e.ExecutablePath.ToLowerInvariant())
                .Select(g => g.First())
                .ToList();

            _indexed = true;
        }
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchResultItem>();

        var lowerQuery = query.Trim().ToLowerInvariant();

        var results = await Task.Run(() => _appIndex
            .Select(app =>
            {
                var lowerName = app.Name.ToLowerInvariant();
                int score = 0;

                if (lowerName == lowerQuery)
                    score = 100;
                else if (lowerName.StartsWith(lowerQuery))
                    score = 80;
                else if (lowerName.Contains(lowerQuery))
                    score = 60;
                else if (app.ExecutablePath.ToLowerInvariant().Contains(lowerQuery))
                    score = 30;

                return new { App = app, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.App.Name.Length)
            .Take(12)
            .Select(x => new SearchResultItem
            {
                Title = x.App.Name,
                Subtitle = TruncatePath(x.App.ExecutablePath, 60),
                Tag = "APP",
                ExecutePath = x.App.ExecutablePath,
                Arguments = x.App.Arguments,
                Icon = GetCachedIcon(x.App.ExecutablePath),
                Score = x.Score
            })
            .ToList(), ct);

        return results;
    }

    private static AppEntry? ResolveShortcut(string lnkPath)
    {
        try
        {
            var resolved = ShortcutResolver.Resolve(lnkPath);
            if (resolved == null) return null;

            var (targetPath, arguments) = resolved.Value;

            if (string.IsNullOrEmpty(targetPath))
                return null;

            // 只保留 .exe
            if (!targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return null;

            // 排除系统卸载程序等
            var name = Path.GetFileNameWithoutExtension(lnkPath);
            if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("卸载", StringComparison.OrdinalIgnoreCase))
                return null;

            return new AppEntry
            {
                Name = name,
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

    private static ImageSource? ExtractIcon(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return null;

            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;

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

    private ImageSource? GetCachedIcon(string exePath)
    {
        lock (_lock)
        {
            if (_iconCache.TryGetValue(exePath, out var cachedIcon))
            {
                return cachedIcon;
            }
        }

        var icon = ExtractIcon(exePath);

        lock (_lock)
        {
            _iconCache[exePath] = icon;
        }

        return icon;
    }

    private static string TruncatePath(string path, int maxLength)
    {
        if (path.Length <= maxLength) return path;
        return "..." + path[^(maxLength - 3)..];
    }
}
