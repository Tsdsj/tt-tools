using System.Windows;
using TtLauncher.Commands;
using TtLauncher.Providers;
using TtLauncher.Services;
using TtLauncher.ViewModels;
using TtLauncher.Views;
using Application = System.Windows.Application;

namespace TtLauncher;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 组装依赖
        var appProvider = new AppSearchProvider();
        var router = new CommandRouter();
        router.RegisterDefault(appProvider);

        // 后续扩展点：在此注册更多 Provider
        // router.Register("f", new EverythingSearchProvider());
        // router.Register("ocr", new OcrProvider());
        // router.Register("port", new PortQueryProvider());
        // router.Register("ports", new PortListProvider());

        var indexService = new AppIndexService(appProvider);
        var hotkeyService = new HotkeyService();
        var viewModel = new MainViewModel(router, indexService);
        var mainWindow = new MainWindow(viewModel, hotkeyService);

        // 托盘图标
        SetupTrayIcon(mainWindow);

        mainWindow.Show();
    }

    private void SetupTrayIcon(MainWindow window)
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

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

