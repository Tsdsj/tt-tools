using TtLauncher.Models;
using TtLauncher.Services;

namespace TtLauncher.Providers;

/// <summary>
/// OCR 搜索 Provider
/// </summary>
public class OcrSearchProvider : ISearchProvider
{
    private readonly OcrService _ocrService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public OcrSearchProvider(OcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public string Name => "OCR 截图识别";

    public string? CommandPrefix => "ocr";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!await _semaphore.WaitAsync(0, ct))
        {
            return
            [
                SearchResultItem.CreateInfo("OCR 正在处理中", "请等待当前截图识别流程完成。", "OCR")
            ];
        }

        try
        {
            var captureResult = await _ocrService.CaptureAndRecognizeAsync(ct);
            if (captureResult.IsCanceled)
            {
                return
                [
                    SearchResultItem.CreateInfo("已取消截图", "重新输入 ocr 可再次开始截图识别。", "OCR")
                ];
            }

            if (!captureResult.IsSuccess)
            {
                return
                [
                    SearchResultItem.CreateInfo("OCR 识别失败", captureResult.ErrorMessage, "OCR")
                ];
            }

            var previewText = NormalizePreview(captureResult.Text);
            var title = GetTitleFromText(captureResult.Text);

            return
            [
                new SearchResultItem
                {
                    Title = title,
                    Subtitle = previewText,
                    Tag = "COPY",
                    Score = 100,
                    Kind = SearchResultKind.OcrText,
                    ExecuteAsync = _ =>
                    {
                        System.Windows.Clipboard.SetText(captureResult.Text);
                        return Task.FromResult(SearchResultActionResult.Status("OCR 文本已复制到剪贴板"));
                    }
                }
            ];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string GetTitleFromText(string text)
    {
        var firstLine = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return "OCR 识别结果";
        }

        return firstLine.Length > 42 ? $"{firstLine[..42]}..." : firstLine;
    }

    private static string NormalizePreview(string text)
    {
        var normalized = string.Join(" ", text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length > 140)
        {
            return $"{normalized[..140]}...";
        }

        return normalized;
    }
}
