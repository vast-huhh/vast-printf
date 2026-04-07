using System.Collections.Generic;

namespace VastPrint.App.Services;

public static class ExtensionDescriptionProvider
{
    private static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "纯文本",
        [".log"] = "日志文本",
        [".csv"] = "CSV 文本",
        [".json"] = "JSON 文本",
        [".xml"] = "XML 文本",
        [".md"] = "Markdown 文本",
        [".ini"] = "配置文本",
        [".rtf"] = "RTF 富文本",
        [".png"] = "PNG 图像",
        [".jpg"] = "JPEG 图像",
        [".jpeg"] = "JPEG 图像",
        [".bmp"] = "BMP 图像",
        [".gif"] = "GIF 图像",
        [".tif"] = "TIFF 图像",
        [".tiff"] = "TIFF 图像",
        [".pdf"] = "PDF 文档",
        [".xps"] = "XPS 文档",
        [".doc"] = "Word 文档",
        [".docx"] = "Word 文档",
        [".xls"] = "Excel 表格",
        [".xlsx"] = "Excel 表格",
        [".ppt"] = "PowerPoint 演示文稿",
        [".pptx"] = "PowerPoint 演示文稿",
    };

    public static string Describe(string extension)
    {
        return Descriptions.TryGetValue(extension, out var description) ? description : "文件";
    }
}
