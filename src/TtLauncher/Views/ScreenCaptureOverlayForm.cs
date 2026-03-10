using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Bitmap = System.Drawing.Bitmap;
using Brush = System.Drawing.Brush;
using Brushes = System.Drawing.Brushes;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace TtLauncher.Views;

/// <summary>
/// 截图选区遮罩层
/// </summary>
public class ScreenCaptureOverlayForm : Form
{
    private static readonly Pen SelectionBorderPen = new(Color.FromArgb(160, 214, 230, 255), 2f);
    private static readonly Brush MaskBrush = new SolidBrush(Color.FromArgb(148, 10, 12, 16));
    private static readonly Brush HintBackgroundBrush = new SolidBrush(Color.FromArgb(190, 20, 22, 27));
    private static readonly Brush SelectionFillBrush = new SolidBrush(Color.FromArgb(34, 142, 181, 255));
    private static readonly Brush BorderShadowBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255));
    private static readonly Font HintFont = new("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);

    private readonly Bitmap _screenBitmap;
    private readonly Rectangle _virtualBounds;
    private Point _dragStartPoint;
    private Rectangle _selectionRectangle;
    private bool _isDragging;

    private ScreenCaptureOverlayForm(Bitmap screenBitmap, Rectangle virtualBounds)
    {
        _screenBitmap = screenBitmap;
        _virtualBounds = virtualBounds;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualBounds;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        BackColor = Color.Black;
    }

    /// <summary>
    /// 选择截图区域
    /// </summary>
    /// <returns>截图位图，取消时返回 null</returns>
    public static Bitmap? CaptureSelection()
    {
        var virtualBounds = SystemInformation.VirtualScreen;
        if (virtualBounds.Width <= 0 || virtualBounds.Height <= 0)
        {
            return null;
        }

        using var screenBitmap = new Bitmap(virtualBounds.Width, virtualBounds.Height);
        using (var graphics = Graphics.FromImage(screenBitmap))
        {
            graphics.CopyFromScreen(virtualBounds.Location, Point.Empty, virtualBounds.Size);
        }

        using var overlay = new ScreenCaptureOverlayForm((Bitmap)screenBitmap.Clone(), virtualBounds);
        return overlay.ShowDialog() == DialogResult.OK
            ? overlay.CreateSelectionBitmap()
            : null;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Focus();
        Activate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImageUnscaled(_screenBitmap, Point.Empty);
        e.Graphics.FillRectangle(MaskBrush, ClientRectangle);

        if (_selectionRectangle.Width > 0 && _selectionRectangle.Height > 0)
        {
            e.Graphics.DrawImage(_screenBitmap, _selectionRectangle, _selectionRectangle, GraphicsUnit.Pixel);

            using var shadowPath = CreateRoundedRectangle(_selectionRectangle, 10);
            e.Graphics.FillPath(BorderShadowBrush, shadowPath);
            e.Graphics.FillRectangle(SelectionFillBrush, _selectionRectangle);
            using var borderPath = CreateRoundedRectangle(_selectionRectangle, 10);
            e.Graphics.DrawPath(SelectionBorderPen, borderPath);

            var sizeLabel = $"{_selectionRectangle.Width} × {_selectionRectangle.Height}";
            var sizePosition = new PointF(_selectionRectangle.Left + 12, Math.Max(12, _selectionRectangle.Top - 30));
            e.Graphics.DrawString(sizeLabel, HintFont, Brushes.White, sizePosition);
        }

        DrawHint(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _isDragging = true;
        _dragStartPoint = e.Location;
        _selectionRectangle = Rectangle.Empty;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging)
        {
            return;
        }

        _selectionRectangle = NormalizeRectangle(_dragStartPoint, e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left || !_isDragging)
        {
            return;
        }

        _isDragging = false;
        _selectionRectangle = NormalizeRectangle(_dragStartPoint, e.Location);
        if (_selectionRectangle.Width < 8 || _selectionRectangle.Height < 8)
        {
            _selectionRectangle = Rectangle.Empty;
            Invalidate();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _screenBitmap.Dispose();
        }

        base.Dispose(disposing);
    }

    private Bitmap CreateSelectionBitmap()
    {
        var cropRectangle = _selectionRectangle;
        cropRectangle.Intersect(new Rectangle(Point.Empty, _screenBitmap.Size));

        var result = new Bitmap(cropRectangle.Width, cropRectangle.Height);
        using var graphics = Graphics.FromImage(result);
        graphics.DrawImage(_screenBitmap, new Rectangle(0, 0, result.Width, result.Height), cropRectangle, GraphicsUnit.Pixel);
        return result;
    }

    private void DrawHint(Graphics graphics)
    {
        const string hintText = "拖拽选择区域进行 OCR，按 Esc 取消";
        var hintSize = graphics.MeasureString(hintText, HintFont);
        var hintRectangle = new RectangleF(24, 24, hintSize.Width + 24, hintSize.Height + 14);
        graphics.FillRoundedRectangle(HintBackgroundBrush, hintRectangle, 12);
        graphics.DrawString(hintText, HintFont, Brushes.WhiteSmoke, hintRectangle.Left + 12, hintRectangle.Top + 7);
    }

    private static Rectangle NormalizeRectangle(Point startPoint, Point endPoint)
    {
        var left = Math.Min(startPoint.X, endPoint.X);
        var top = Math.Min(startPoint.Y, endPoint.Y);
        var width = Math.Abs(endPoint.X - startPoint.X);
        var height = Math.Abs(endPoint.Y - startPoint.Y);
        return new Rectangle(left, top, width, height);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class GraphicsExtensions
{
    /// <summary>
    /// 绘制圆角矩形填充
    /// </summary>
    /// <param name="graphics">画布</param>
    /// <param name="brush">画刷</param>
    /// <param name="rectangle">矩形</param>
    /// <param name="radius">圆角半径</param>
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF rectangle, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        graphics.FillPath(brush, path);
    }
}
