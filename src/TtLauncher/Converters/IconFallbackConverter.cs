using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace TtLauncher.Converters;

/// <summary>
/// 图标转换器 — 当 Icon 为 null 时返回默认图标
/// </summary>
public class IconFallbackConverter : IValueConverter
{
    private static readonly ImageSource DefaultIcon = CreateDefaultIcon();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value as ImageSource ?? DefaultIcon;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }

    private static ImageSource CreateDefaultIcon()
    {
        // 创建一个简单的默认图标（灰色圆形）
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(100, 100, 120)),
                null,
                new Point(16, 16),
                14, 14);

            // 中间画一个小矩形代表应用
            ctx.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(180, 180, 200)),
                null,
                new Rect(9, 9, 14, 14),
                2, 2);
        }

        var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }
}
