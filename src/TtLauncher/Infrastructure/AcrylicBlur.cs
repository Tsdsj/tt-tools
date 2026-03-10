using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TtLauncher.Infrastructure;

/// <summary>
/// 使用 Win32 API 实现窗口背景模糊（Acrylic / Blur Behind）
/// 支持 Windows 10 1903+ 和 Windows 11
/// </summary>
public static class AcrylicBlur
{
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public uint AccentFlags;
        public uint GradientColor; // AABBGGRR 格式
        public uint AnimationId;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // Win10 1903+
        ACCENT_INVALID_STATE = 5
    }

    /// <summary>
    /// 启用窗口的 Acrylic 背景模糊效果
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="tintColor">叠加色调 (ARGB)</param>
    public static void Enable(Window window, uint tintColor = 0x80000000)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        // 先尝试 Acrylic (Win10 1903+)，失败则回退到 BlurBehind
        if (!TrySetAccent(handle, AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND, tintColor))
        {
            TrySetAccent(handle, AccentState.ACCENT_ENABLE_BLURBEHIND, tintColor);
        }
    }

    public static void SetRoundedRegion(IntPtr handle, int width, int height, int cornerRadius)
    {
        if (handle == IntPtr.Zero || width <= 0 || height <= 0)
        {
            return;
        }

        var diameter = Math.Max(1, cornerRadius * 2);
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
        if (region != IntPtr.Zero)
        {
            SetWindowRgn(handle, region, true);
        }
    }

    private static bool TrySetAccent(IntPtr handle, AccentState state, uint gradientColor)
    {
        try
        {
            var accent = new AccentPolicy
            {
                AccentState = state,
                AccentFlags = 2, // ACCENT_FLAG_DRAW_ALL
                GradientColor = gradientColor
            };

            var accentSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentSize,
                Data = accentPtr
            };

            var result = SetWindowCompositionAttribute(handle, ref data);
            Marshal.FreeHGlobal(accentPtr);

            return result != 0;
        }
        catch
        {
            return false;
        }
    }
}
