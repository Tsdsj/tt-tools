using Microsoft.Win32;

namespace TtLauncher.Services;

/// <summary>
/// 开机自启动服务
/// </summary>
public class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "TtLauncher";

    /// <summary>
    /// 判断是否已启用开机自启动
    /// </summary>
    /// <returns>是否启用</returns>
    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        var value = runKey?.GetValue(AppName)?.ToString();
        return string.Equals(value, BuildCommandLine(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 设置开机自启动状态
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

        if (enabled)
        {
            runKey.SetValue(AppName, BuildCommandLine());
            return;
        }

        runKey.DeleteValue(AppName, false);
    }

    private static string BuildCommandLine()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("无法确定当前程序路径，不能设置开机自启动。");
        }

        return $"\"{processPath}\"";
    }
}
