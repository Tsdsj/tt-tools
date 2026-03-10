using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using TtLauncher.Infrastructure;
using TtLauncher.Services;
using TtLauncher.ViewModels;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TtLauncher.Views;

public partial class MainWindow : Window
{
    private const int WindowCornerRadius = 24;
    private const int WmDpiChanged = 0x02E0;
    private readonly MainViewModel _viewModel;
    private readonly HotkeyService _hotkeyService;
    private bool _hasWindowPosition;
    private bool _resetOnNextShow = true;
    private HwndSource? _hwndSource;

    public MainWindow(MainViewModel viewModel, HotkeyService hotkeyService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        SizeChanged += OnWindowSizeChanged;
        LocationChanged += OnWindowLocationChanged;
        _viewModel.RequestHide += HideWindow;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Register(handle);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        HideWindow(false);
        await _viewModel.InitializeAsync();
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            if (IsVisible)
            {
                HideWindow(true);
            }
            else
            {
                ShowWindow();
            }
        });
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
        UpdateWindowBoundsForCurrentScreen();
        ApplyRoundedRegion();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyRoundedRegion();
    }

    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateWindowBoundsForCurrentScreen();
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

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        var cornerRadius = (int)Math.Ceiling(WindowCornerRadius * Math.Max(dpi.DpiScaleX, dpi.DpiScaleY));
        AcrylicBlur.SetRoundedRegion(handle, width, height, cornerRadius);
    }

    private void ShowWindow()
    {
        if (_resetOnNextShow)
        {
            _viewModel.Reset();
        }

        if (!_hasWindowPosition)
        {
            CenterOnScreen();
            _hasWindowPosition = true;
        }

        Show();
        Activate();
        SearchBox.Focus();
        SearchBox.Select(SearchBox.Text.Length, 0);
        _resetOnNextShow = false;
    }

    private void HideWindow()
    {
        HideWindow(false);
    }

    private void HideWindow(bool preserveState)
    {
        _resetOnNextShow = !preserveState;
        Hide();
    }

    private void CenterOnScreen()
    {
        var screen = GetCurrentScreen();
        var dpi = VisualTreeHelper.GetDpi(this);
        var workWidth = screen.WorkingArea.Width / dpi.DpiScaleX;
        var workHeight = screen.WorkingArea.Height / dpi.DpiScaleY;
        var workLeft = screen.WorkingArea.Left / dpi.DpiScaleX;
        var workTop = screen.WorkingArea.Top / dpi.DpiScaleY;

        Left = workLeft + (workWidth - Width) / 2;
        Top = workTop + Math.Max(40, workHeight * 0.2);
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideWindow(true);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideWindow(false);
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

    private void RootBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source || IsInteractiveSource(source))
        {
            return;
        }

        try
        {
            DragMove();
            _hasWindowPosition = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool IsInteractiveSource(DependencyObject source)
    {
        return HasAncestor<System.Windows.Controls.TextBox>(source)
               || HasAncestor<System.Windows.Controls.Primitives.TextBoxBase>(source)
               || HasAncestor<System.Windows.Controls.ListBox>(source)
               || HasAncestor<System.Windows.Controls.ListBoxItem>(source)
               || HasAncestor<System.Windows.Controls.Primitives.ScrollBar>(source);
    }

    private static bool HasAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }

    private void UpdateWindowBoundsForCurrentScreen()
    {
        var screen = GetCurrentScreen();
        var dpi = VisualTreeHelper.GetDpi(this);
        var availableWidth = screen.WorkingArea.Width / dpi.DpiScaleX;
        var availableHeight = screen.WorkingArea.Height / dpi.DpiScaleY;
        var workLeft = screen.WorkingArea.Left / dpi.DpiScaleX;
        var workTop = screen.WorkingArea.Top / dpi.DpiScaleY;
        var workRight = workLeft + availableWidth;
        var workBottom = workTop + availableHeight;

        MaxWidth = Math.Max(720, availableWidth - 48);
        MaxHeight = Math.Max(420, availableHeight - 72);

        if (Width > MaxWidth)
        {
            Width = MaxWidth;
        }

        if (!_hasWindowPosition)
        {
            return;
        }

        if (Left + ActualWidth > workRight - 16)
        {
            Left = Math.Max(workLeft + 16, workRight - ActualWidth - 16);
        }

        if (Top + ActualHeight > workBottom - 16)
        {
            Top = Math.Max(workTop + 16, workBottom - ActualHeight - 16);
        }

        if (Left < workLeft + 16)
        {
            Left = workLeft + 16;
        }

        if (Top < workTop + 16)
        {
            Top = workTop + 16;
        }
    }

    private System.Windows.Forms.Screen GetCurrentScreen()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            return System.Windows.Forms.Screen.FromHandle(handle);
        }

        return System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmDpiChanged)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateWindowBoundsForCurrentScreen();
                ApplyRoundedRegion();
            });
        }

        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        SizeChanged -= OnWindowSizeChanged;
        LocationChanged -= OnWindowLocationChanged;
        _hwndSource?.RemoveHook(WndProc);
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Dispose();
        _viewModel.RequestHide -= HideWindow;
        base.OnClosed(e);
    }
}
