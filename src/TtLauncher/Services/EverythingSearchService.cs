using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace TtLauncher.Services;

/// <summary>
/// Everything 文件搜索服务
/// </summary>
public class EverythingSearchService
{
    private static readonly string[] CandidateRegistryKeys =
    [
        @"HKEY_LOCAL_MACHINE\Software\voidtools\Everything",
        @"HKEY_CURRENT_USER\Software\voidtools\Everything",
        @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\voidtools\Everything",
        @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
        @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
        @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\Everything.exe",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\Everything.exe"
    ];

    private readonly TimeSpan _searchTimeout = TimeSpan.FromSeconds(3);
    private readonly TimeSpan _installInfoCacheDuration = TimeSpan.FromMinutes(2);
    private EverythingInstallationInfo? _cachedInstallationInfo;
    private DateTimeOffset _installInfoCachedAt = DateTimeOffset.MinValue;

    /// <summary>
    /// 执行 Everything 搜索
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="maxResults">最多返回条数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>搜索结果</returns>
    public async Task<EverythingSearchResponse> SearchAsync(string keyword, int maxResults = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return EverythingSearchResponse.Fail("请输入文件关键词", "示例：f readme 或 f config.json");
        }

        var installInfo = GetInstallationInfo();
        if (!installInfo.IsInstalled)
        {
            return EverythingSearchResponse.Fail(
                "未检测到 Everything",
                "请先安装 Everything，再使用 f 关键词 进行文件搜索。");
        }

        if (string.IsNullOrWhiteSpace(installInfo.EsPath))
        {
            var locationText = string.IsNullOrWhiteSpace(installInfo.InstallLocation)
                ? "已检测到 Everything，但当前环境里没有可用的 es.exe。"
                : $"已检测到 Everything，安装目录：{installInfo.InstallLocation}，但未找到可用的 es.exe。";

            return EverythingSearchResponse.Fail(
                "Everything 已安装，但缺少 es.exe",
                $"{locationText} 打包时可将 es.exe 内置到应用目录的 tools\\everything\\ 下，或手动配置 EVERYTHING_ES_PATH。");
        }

        var exportFilePath = Path.Combine(
            Path.GetTempPath(),
            $"ttlauncher-everything-{Guid.NewGuid():N}.txt");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = installInfo.EsPath,
                Arguments = BuildArguments(keyword.Trim(), maxResults, exportFilePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.Default
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            TryDeleteFile(exportFilePath);
            return EverythingSearchResponse.Fail("Everything 调用失败", ex.Message);
        }

        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_searchTimeout);
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKillProcess(process);
            TryDeleteFile(exportFilePath);
            return EverythingSearchResponse.Fail("Everything 搜索超时", "请确认 Everything 正在运行，或缩小搜索范围后重试。");
        }
        catch (Exception ex)
        {
            TryKillProcess(process);
            TryDeleteFile(exportFilePath);
            return EverythingSearchResponse.Fail("Everything 搜索失败", ex.Message);
        }

        var standardError = await errorTask;
        if (process.ExitCode != 0)
        {
            TryDeleteFile(exportFilePath);
            var errorMessage = string.IsNullOrWhiteSpace(standardError)
                ? $"es.exe 退出码：{process.ExitCode}"
                : standardError.Trim();

            return EverythingSearchResponse.Fail("Everything 返回异常", errorMessage);
        }

        string standardOutput;
        try
        {
            if (!File.Exists(exportFilePath))
            {
                return EverythingSearchResponse.Fail("Everything 返回异常", "未生成搜索结果文件，请稍后重试。");
            }

            standardOutput = await File.ReadAllTextAsync(exportFilePath, Encoding.UTF8, ct);
        }
        catch (Exception ex)
        {
            return EverythingSearchResponse.Fail("Everything 结果读取失败", ex.Message);
        }
        finally
        {
            TryDeleteFile(exportFilePath);
        }

        var entries = ParseEntries(standardOutput).Take(maxResults).ToList();
        return EverythingSearchResponse.Success(entries);
    }

    private EverythingInstallationInfo GetInstallationInfo()
    {
        if (_cachedInstallationInfo is not null && DateTimeOffset.UtcNow - _installInfoCachedAt < _installInfoCacheDuration)
        {
            return _cachedInstallationInfo;
        }

        _cachedInstallationInfo = DetectInstallation();
        _installInfoCachedAt = DateTimeOffset.UtcNow;
        return _cachedInstallationInfo;
    }

    private static EverythingInstallationInfo DetectInstallation()
    {
        var installDirectories = new List<string>();
        var bundledEsPath = ResolveBundledEsPath();
        if (!string.IsNullOrWhiteSpace(bundledEsPath))
        {
            return new EverythingInstallationInfo(true, Path.GetDirectoryName(bundledEsPath), bundledEsPath, true);
        }

        var envEsPath = Environment.GetEnvironmentVariable("EVERYTHING_ES_PATH");
        if (!string.IsNullOrWhiteSpace(envEsPath))
        {
            if (File.Exists(envEsPath))
            {
                return new EverythingInstallationInfo(true, Path.GetDirectoryName(envEsPath), envEsPath, false);
            }

            var envDirectory = Directory.Exists(envEsPath) ? envEsPath : Path.GetDirectoryName(envEsPath);
            if (!string.IsNullOrWhiteSpace(envDirectory))
            {
                installDirectories.Add(envDirectory);
            }
        }

        installDirectories.AddRange(GetRegistryDirectories());
        installDirectories.AddRange(GetRunningProcessDirectories());
        installDirectories.AddRange(GetPathDirectories());
        installDirectories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything"));
        installDirectories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything"));
        installDirectories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Everything"));

        var distinctDirectories = installDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var directory in distinctDirectories)
        {
            var esPath = Path.Combine(directory, "es.exe");
            if (File.Exists(esPath))
            {
                return new EverythingInstallationInfo(true, directory, esPath, false);
            }
        }

        var installLocation = distinctDirectories.FirstOrDefault(Directory.Exists);
        return new EverythingInstallationInfo(installLocation is not null, installLocation, null, false);
    }

    private static string? ResolveBundledEsPath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDirectory, "tools", "everything", "es.exe"),
            Path.Combine(baseDirectory, "Assets", "Tools", "Everything", "es.exe"),
            Path.Combine(baseDirectory, "es.exe")
        };

        return candidatePaths.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> GetRegistryDirectories()
    {
        foreach (var keyPath in CandidateRegistryKeys)
        {
            object? installLocation = null;
            object? displayIcon = null;
            object? defaultValue = null;

            try
            {
                installLocation = Registry.GetValue(keyPath, "InstallLocation", null);
                displayIcon = Registry.GetValue(keyPath, "DisplayIcon", null);
                defaultValue = Registry.GetValue(keyPath, null, null);
            }
            catch
            {
                // 忽略不可访问的注册表项
            }

            foreach (var value in new[] { installLocation, displayIcon, defaultValue })
            {
                var directory = NormalizeDirectory(value?.ToString());
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    yield return directory;
                }
            }
        }
    }

    private static IEnumerable<string> GetRunningProcessDirectories()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("Everything");
        }
        catch
        {
            yield break;
        }

        foreach (var process in processes)
        {
            string? path = null;

            try
            {
                path = process.MainModule?.FileName;
            }
            catch
            {
                path = null;
            }
            finally
            {
                process.Dispose();
            }

            var directory = NormalizeDirectory(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return directory;
            }
        }
    }

    private static IEnumerable<string> GetPathDirectories()
    {
        var pathEnvironment = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathEnvironment.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return directory;
            }
        }
    }

    private static string? NormalizeDirectory(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var normalized = rawValue.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(normalized);
        }

        return normalized;
    }

    private static string BuildArguments(string keyword, int maxResults, string exportFilePath)
    {
        var escapedKeyword = keyword.Replace("\"", "\"\"");
        var escapedExportPath = exportFilePath.Replace("\"", "\"\"");
        return $"-export-txt \"{escapedExportPath}\" -utf8-bom -n {maxResults} \"{escapedKeyword}\"";
    }

    private static IEnumerable<EverythingSearchEntry> ParseEntries(string standardOutput)
    {
        foreach (var line in standardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fullPath = line.Trim();
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                continue;
            }

            var isDirectory = Directory.Exists(fullPath);
            var isFile = File.Exists(fullPath);

            if (!isDirectory && !isFile)
            {
                isDirectory = !Path.HasExtension(fullPath);
            }

            var title = isDirectory
                ? GetDirectoryName(fullPath)
                : Path.GetFileName(fullPath);

            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (isDirectory && string.IsNullOrWhiteSpace(parentDirectory))
            {
                parentDirectory = fullPath;
            }

            yield return new EverythingSearchEntry
            {
                FullPath = fullPath,
                Name = string.IsNullOrWhiteSpace(title) ? fullPath : title,
                ParentDirectory = parentDirectory ?? string.Empty,
                IsDirectory = isDirectory
            };
        }
    }

    private static string GetDirectoryName(string directoryPath)
    {
        var normalizedPath = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(normalizedPath);
        return string.IsNullOrWhiteSpace(name) ? normalizedPath : name;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // 忽略进程清理失败
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // 忽略临时文件清理失败
        }
    }
}

/// <summary>
/// Everything 安装信息
/// </summary>
public sealed record EverythingInstallationInfo(bool IsInstalled, string? InstallLocation, string? EsPath, bool IsBundled);

/// <summary>
/// Everything 搜索响应
/// </summary>
public sealed class EverythingSearchResponse
{
    public bool IsSuccess { get; init; }

    public IReadOnlyList<EverythingSearchEntry> Entries { get; init; } = Array.Empty<EverythingSearchEntry>();

    public string MessageTitle { get; init; } = string.Empty;

    public string MessageSubtitle { get; init; } = string.Empty;

    public static EverythingSearchResponse Success(IReadOnlyList<EverythingSearchEntry> entries)
    {
        return new EverythingSearchResponse
        {
            IsSuccess = true,
            Entries = entries
        };
    }

    public static EverythingSearchResponse Fail(string title, string subtitle)
    {
        return new EverythingSearchResponse
        {
            IsSuccess = false,
            MessageTitle = title,
            MessageSubtitle = subtitle
        };
    }
}

/// <summary>
/// Everything 搜索条目
/// </summary>
public sealed class EverythingSearchEntry
{
    public string FullPath { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ParentDirectory { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }
}
