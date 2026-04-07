using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace VastPrint.App.Services;

public sealed class OfficeDocumentPdfConverter
{
    private readonly LogService _logService;

    public OfficeDocumentPdfConverter(LogService logService)
    {
        _logService = logService;
    }

    public string ConvertToPdf(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "VastPrint", "OfficeExports");
        Directory.CreateDirectory(tempDirectory);
        var tempPdfPath = Path.Combine(
            tempDirectory,
            $"{Path.GetFileNameWithoutExtension(filePath)}-{Guid.NewGuid():N}.pdf");

        _logService.Info($"Office convert start: file='{filePath}', extension='{extension}', output='{tempPdfPath}'");

        var libreOfficePath = ResolveLibreOfficeExecutablePath();
        if (!string.IsNullOrWhiteSpace(libreOfficePath))
        {
            try
            {
                ConvertWithLibreOffice(libreOfficePath, filePath, tempPdfPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logService.Warning($"LibreOffice convert failed, fallback to Office COM. file='{filePath}', details={ex.Message}");
            }
        }

        if (!File.Exists(tempPdfPath))
        {
            ConvertWithOfficeInterop(extension, filePath, tempPdfPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(tempPdfPath))
        {
            throw new FileNotFoundException("Office 转换完成后未生成 PDF 文件。", tempPdfPath);
        }

        _logService.Info($"Office convert completed: file='{filePath}', output='{tempPdfPath}', size={new FileInfo(tempPdfPath).Length}");
        return tempPdfPath;
    }

    private void ConvertWithLibreOffice(string sofficePath, string sourcePath, string outputPath, CancellationToken cancellationToken)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("无法确定 PDF 输出目录。");

        var libreOfficeProfile = Path.Combine(Path.GetTempPath(), "VastPrint", "LibreOfficeProfile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(libreOfficeProfile);

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = sofficePath,
                Arguments =
                    $"--headless --nologo --nodefault --nolockcheck --norestore " +
                    $"-env:UserInstallation={new Uri(libreOfficeProfile).AbsoluteUri} " +
                    $"--convert-to pdf --outdir \"{outputDirectory}\" \"{sourcePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _logService.Info($"LibreOffice convert start: soffice='{sofficePath}', source='{sourcePath}'");

            process.Start();
            var exited = process.WaitForExit(120000);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                throw new TimeoutException("LibreOffice 转换超时。");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            _logService.Info($"LibreOffice convert exit: code={process.ExitCode}, stdout='{stdout}', stderr='{stderr}'");

            var defaultPdfPath = Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(sourcePath)}.pdf");

            if (File.Exists(defaultPdfPath) && !defaultPdfPath.Equals(outputPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.Move(defaultPdfPath, outputPath);
            }

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new InvalidOperationException("LibreOffice 未成功生成 PDF。");
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(libreOfficeProfile))
                {
                    Directory.Delete(libreOfficeProfile, true);
                }
            }
            catch
            {
            }
        }
    }

    private void ConvertWithOfficeInterop(string extension, string sourcePath, string outputPath)
    {
        switch (extension)
        {
            case ".doc":
            case ".docx":
                ConvertWordToPdf(sourcePath, outputPath);
                break;
            case ".xls":
            case ".xlsx":
                ConvertExcelToPdf(sourcePath, outputPath);
                break;
            case ".ppt":
            case ".pptx":
                ConvertPowerPointToPdf(sourcePath, outputPath);
                break;
            default:
                throw new NotSupportedException($"暂不支持将 {extension} 转换为 PDF。");
        }
    }

    private static void ConvertWordToPdf(string sourcePath, string outputPath)
    {
        dynamic? wordApp = null;
        dynamic? document = null;

        try
        {
            wordApp = CreateOfficeApplication("Word.Application", "Microsoft Word");
            wordApp.Visible = false;
            wordApp.DisplayAlerts = 0;

            document = wordApp.Documents.Open(sourcePath, false, true, false);
            document.ExportAsFixedFormat(
                outputPath,
                17,
                false,
                0,
                0,
                1,
                1,
                0,
                true,
                true,
                0,
                true,
                true,
                false,
                Type.Missing);
        }
        finally
        {
            TryCloseWordDocument(document);
            TryQuitApplication(wordApp);
            ReleaseComObject(document);
            ReleaseComObject(wordApp);
        }
    }

    private static void ConvertExcelToPdf(string sourcePath, string outputPath)
    {
        dynamic? excelApp = null;
        dynamic? workbook = null;

        try
        {
            excelApp = CreateOfficeApplication("Excel.Application", "Microsoft Excel");
            excelApp.Visible = false;
            excelApp.DisplayAlerts = false;
            excelApp.ScreenUpdating = false;

            workbook = excelApp.Workbooks.Open(sourcePath, 0, true);
            workbook.ExportAsFixedFormat(
                0,
                outputPath,
                0,
                true,
                false,
                Type.Missing,
                Type.Missing,
                false,
                Type.Missing);
        }
        finally
        {
            TryCloseExcelWorkbook(workbook);
            TryQuitApplication(excelApp);
            ReleaseComObject(workbook);
            ReleaseComObject(excelApp);
        }
    }

    private static void ConvertPowerPointToPdf(string sourcePath, string outputPath)
    {
        dynamic? powerPointApp = null;
        dynamic? presentation = null;

        try
        {
            powerPointApp = CreateOfficeApplication("PowerPoint.Application", "Microsoft PowerPoint");
            presentation = powerPointApp.Presentations.Open(sourcePath, -1, 0, 0);
            presentation.SaveAs(outputPath, 32);
        }
        finally
        {
            TryClosePowerPointPresentation(presentation);
            TryQuitApplication(powerPointApp);
            ReleaseComObject(presentation);
            ReleaseComObject(powerPointApp);
        }
    }

    private static dynamic CreateOfficeApplication(string progId, string productName)
    {
        var officeType = Type.GetTypeFromProgID(progId, throwOnError: false);
        if (officeType is null)
        {
            throw new InvalidOperationException($"{productName} 未安装，且未找到 LibreOffice，无法打印此类文档。");
        }

        return Activator.CreateInstance(officeType)
            ?? throw new InvalidOperationException($"无法启动 {productName}。");
    }

    private static string? ResolveLibreOfficeExecutablePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "LibreOffice", "program", "soffice.exe"),
            Path.Combine(AppContext.BaseDirectory, "libreoffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void TryCloseWordDocument(dynamic? document)
    {
        if (document is null)
        {
            return;
        }

        try
        {
            document.Close(false);
        }
        catch
        {
        }
    }

    private static void TryCloseExcelWorkbook(dynamic? workbook)
    {
        if (workbook is null)
        {
            return;
        }

        try
        {
            workbook.Close(false);
        }
        catch
        {
        }
    }

    private static void TryClosePowerPointPresentation(dynamic? presentation)
    {
        if (presentation is null)
        {
            return;
        }

        try
        {
            presentation.Close();
        }
        catch
        {
        }
    }

    private static void TryQuitApplication(dynamic? application)
    {
        if (application is null)
        {
            return;
        }

        try
        {
            application.Quit();
        }
        catch
        {
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(instance);
        }
        catch
        {
        }
    }
}
