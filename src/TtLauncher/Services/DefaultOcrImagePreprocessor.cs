using Bitmap = System.Drawing.Bitmap;

namespace TtLauncher.Services;

/// <summary>
/// 默认 OCR 图像预处理器
/// </summary>
public class DefaultOcrImagePreprocessor : IOcrImagePreprocessor
{
    /// <inheritdoc />
    public Bitmap Preprocess(Bitmap bitmap)
    {
        return (Bitmap)bitmap.Clone();
    }
}
