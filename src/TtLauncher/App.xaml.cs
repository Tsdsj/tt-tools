using System.Windows;
using TtLauncher.Commands;
using TtLauncher.Providers;
using TtLauncher.Services;
using TtLauncher.ViewModels;
using TtLauncher.Views;
using Application = System.Windows.Application;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace TtLauncher;

/// <summary>
/// 应用程序入口
/// </summary>
public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var appProvider = new AppSearchProvider();
        var everythingSearchService = new EverythingSearchService();
        var portInspectionService = new PortInspectionService();
        var ocrPreprocessor = new DefaultOcrImagePreprocessor();
        var ocrService = new OcrService(ocrPreprocessor);
        var startupService = new StartupService();

        var router = new CommandRouter();
        router.RegisterDefault(appProvider);
        router.Register("f", new EverythingSearchProvider(everythingSearchService));
        router.Register("ocr", new OcrSearchProvider(ocrService));
        router.Register("port", new PortSearchProvider(portInspectionService, false));
        router.Register("ports", new PortSearchProvider(portInspectionService, true));

        var indexService = new AppIndexService(appProvider);
        var hotkeyService = new HotkeyService();
        var viewModel = new MainViewModel(router, indexService);
        var mainWindow = new MainWindow(viewModel, hotkeyService);

        MainWindow = mainWindow;
        SetupTrayIcon(mainWindow, startupService);

        mainWindow.Show();
    }

    private void SetupTrayIcon(MainWindow window, StartupService startupService)
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "TtLauncher - Alt+Space 呼出",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示 (Alt+Space)", null, (_, _) =>
        {
            window.Show();
            window.Activate();
        });

        var startupMenuItem = new ToolStripMenuItem("开机自启动")
        {
            CheckOnClick = true,
            Checked = startupService.IsEnabled()
        };
        startupMenuItem.Click += (_, _) =>
        {
            try
            {
                startupService.SetEnabled(startupMenuItem.Checked);
            }
            catch (Exception ex)
            {
                startupMenuItem.Checked = startupService.IsEnabled();
                _trayIcon?.ShowBalloonTip(2500, "TtLauncher", $"设置开机自启动失败：{ex.Message}", System.Windows.Forms.ToolTipIcon.Warning);
            }
        };

        menu.Items.Add(startupMenuItem);
        menu.Items.Add("-");
        menu.Items.Add("退出", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Shutdown();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) =>
        {
            window.Show();
            window.Activate();
        };
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
