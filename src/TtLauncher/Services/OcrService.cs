using System.IO;
using System.Windows;
using Tesseract;
using TtLauncher.Models;
using TtLauncher.Views;
using Bitmap = System.Drawing.Bitmap;
using Application = System.Windows.Application;

namespace TtLauncher.Services;

/// <summary>
/// OCR 服务
/// </summary>
public class OcrService
{
    private const string OcrLanguages = "chi_sim+eng";
    private readonly IOcrImagePreprocessor _imagePreprocessor;

    public OcrService(IOcrImagePreprocessor imagePreprocessor)
    {
        _imagePreprocessor = imagePreprocessor;
    }

    /// <summary>
    /// 截图并执行 OCR
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>OCR 结果</returns>
    public async Task<OcrCaptureResult> CaptureAndRecognizeAsync(CancellationToken ct = default)
    {
        var tessdataDirectory = ResolveTessdataDirectory();
        if (tessdataDirectory is null)
        {
            return new OcrCaptureResult
            {
                ErrorMessage = "未找到 tessdata 目录。请在程序目录旁放置 tessdata，并包含 chi_sim.traineddata 和 eng.traineddata。"
            };
        }

        var missingLanguages = GetMissingLanguageFiles(tessdataDirectory).ToList();
        if (missingLanguages.Count > 0)
        {
            return new OcrCaptureResult
            {
                ErrorMessage = $"缺少 OCR 语言包：{string.Join("、", missingLanguages)}。请补充到 tessdata 目录。"
            };
        }

        var currentApplication = Application.Current;
        if (currentApplication is null)
        {
            return new OcrCaptureResult
            {
                ErrorMessage = "当前应用上下文不可用，无法启动 OCR。"
            };
        }

        var mainWindow = currentApplication.MainWindow;
        var wasVisible = mainWindow?.IsVisible == true;

        try
        {
            if (wasVisible)
            {
                mainWindow!.Hide();
                await Task.Delay(120, ct);
            }

            var capturedBitmap = await currentApplication.Dispatcher.InvokeAsync(ScreenCaptureOverlayForm.CaptureSelection);
            if (capturedBitmap is null)
            {
                return new OcrCaptureResult
                {
                    IsCanceled = true,
                    ErrorMessage = "已取消截图"
                };
            }

            await using var bitmapScope = new AsyncBitmapScope(capturedBitmap);
            var recognizedText = await Task.Run(() => Recognize(bitmapScope.Bitmap, tessdataDirectory), ct);

            if (string.IsNullOrWhiteSpace(recognizedText))
            {
                return new OcrCaptureResult
                {
                    ErrorMessage = "未识别到可用文本，请尝试选择更清晰的区域。"
                };
            }

            return new OcrCaptureResult
            {
                IsSuccess = true,
                Text = recognizedText.Trim()
            };
        }
        catch (OperationCanceledException)
        {
            return new OcrCaptureResult
            {
                IsCanceled = true,
                ErrorMessage = "OCR 已取消"
            };
        }
        catch (DllNotFoundException)
        {
            return new OcrCaptureResult
            {
                ErrorMessage = "Tesseract 运行库缺失，请确认 NuGet 依赖已正确还原，并使用 x64 环境运行。"
            };
        }
        catch (Exception ex)
        {
            return new OcrCaptureResult
            {
                ErrorMessage = $"OCR 执行失败：{ex.Message}"
            };
        }
        finally
        {
            if (wasVisible)
            {
                mainWindow!.Show();
                mainWindow.Activate();
                mainWindow.Focus();
            }
        }
    }

    private string Recognize(Bitmap bitmap, string tessdataDirectory)
    {
        using var processedBitmap = _imagePreprocessor.Preprocess(bitmap);
        using var memoryStream = new MemoryStream();
        processedBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(memoryStream.ToArray());
        using var engine = new TesseractEngine(tessdataDirectory, OcrLanguages, EngineMode.Default);
        using var page = engine.Process(pix, PageSegMode.Auto);
        return page.GetText() ?? string.Empty;
    }

    private static string? ResolveTessdataDirectory()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var envDirectory = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");

        var candidates = new[]
        {
            Path.Combine(baseDirectory, "tessdata"),
            envDirectory
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));
    }

    private static IEnumerable<string> GetMissingLanguageFiles(string tessdataDirectory)
    {
        var requiredLanguages = new[]
        {
            "chi_sim.traineddata",
            "eng.traineddata"
        };

        foreach (var languageFile in requiredLanguages)
        {
            if (!File.Exists(Path.Combine(tessdataDirectory, languageFile)))
            {
                yield return languageFile;
            }
        }
    }
}

internal sealed class AsyncBitmapScope : IAsyncDisposable
{
    public AsyncBitmapScope(Bitmap bitmap)
    {
        Bitmap = bitmap;
    }

    public Bitmap Bitmap { get; }

    public ValueTask DisposeAsync()
    {
        Bitmap.Dispose();
        return ValueTask.CompletedTask;
    }
}
