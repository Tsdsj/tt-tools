using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TtLauncher.Infrastructure;
using TtLauncher.Services;
using TtLauncher.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace TtLauncher.Views;

public partial class MainWindow : Window
{
    private const int WindowCornerRadius = 24;
    private readonly MainViewModel _viewModel;
    private readonly HotkeyService _hotkeyService;
    private bool _blurApplied;

    public MainWindow(MainViewModel viewModel, HotkeyService hotkeyService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        DataContext = viewModel;

        SourceInitialized += OnSourceInitialized;
        SizeChanged += OnWindowSizeChanged;
        _viewModel.RequestHide += HideWindow;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 注册全局热键
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Register(handle);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // 启用背景模糊（AABBGGRR 格式：半透明深色）
        ApplyBlur();

        // 首次加载时隐藏窗口
        HideWindow();

        await _viewModel.InitializeAsync();
    }

    private void ApplyBlur()
    {
        if (_blurApplied) return;
        // 使用偏深的烟灰蓝 tint，保证浅色桌面上仍有足够对比度
        AcrylicBlur.Enable(this, 0xCC0F1012);
        _blurApplied = true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyRoundedRegion();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        var handle = source.Handle;
        if (handle == IntPtr.Zero || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        AcrylicBlur.SetRoundedRegion(handle, (int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), WindowCornerRadius);
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            if (IsVisible)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        });
    }

    private void ShowWindow()
    {
        _viewModel.Reset();
        Show();
        ApplyBlur();
        Activate();
        CenterOnScreen();
        SearchBox.Focus();
    }

    private void HideWindow()
    {
        Hide();
    }

    private void CenterOnScreen()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = (screenWidth - ActualWidth) / 2;
        // 偏上 1/3 位置，更接近 Raycast/Spotlight 的体验
        Top = screenHeight * 0.25;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // 失焦自动隐藏
        HideWindow();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideWindow();
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.MoveSelectionDownCommand.Execute(null);
                ScrollToSelected();
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.MoveSelectionUpCommand.Execute(null);
                ScrollToSelected();
                e.Handled = true;
                break;

            case Key.Enter:
                _viewModel.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void ScrollToSelected()
    {
        if (_viewModel.SelectedIndex >= 0 && _viewModel.SelectedIndex < ResultList.Items.Count)
        {
            ResultList.ScrollIntoView(ResultList.Items[_viewModel.SelectedIndex]);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        SizeChanged -= OnWindowSizeChanged;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Dispose();
        _viewModel.RequestHide -= HideWindow;
        base.OnClosed(e);
    }
}
