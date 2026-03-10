namespace TtLauncher.Models;

/// <summary>
/// OCR 截图识别结果
/// </summary>
public sealed class OcrCaptureResult
{
    /// <summary>
    /// 是否成功完成识别
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 是否由用户取消
    /// </summary>
    public bool IsCanceled { get; init; }

    /// <summary>
    /// 识别得到的文本
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// 错误提示
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;
}
