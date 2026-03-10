using Bitmap = System.Drawing.Bitmap;

namespace TtLauncher.Services;

/// <summary>
/// OCR 图像预处理接口
/// </summary>
public interface IOcrImagePreprocessor
{
    /// <summary>
    /// 预处理截图
    /// </summary>
    /// <param name="bitmap">原始截图</param>
    /// <returns>处理后的位图</returns>
    Bitmap Preprocess(Bitmap bitmap);
}
