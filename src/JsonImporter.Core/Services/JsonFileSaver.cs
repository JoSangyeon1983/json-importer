using System.IO;
using System.Text;

namespace JsonImporter.Core.Services;

/// <summary>
/// JSON을 UTF-8(BOM 없음)로 파일에 기록합니다.
/// (원본 <c>DataImporter.FileSaver</c>에서 Unity <c>AssetDatabase</c> 의존성과
/// CSV 저장 로직을 제거한 이식본. JSON은 표준 관례에 따라 BOM 없이 저장합니다.)
/// </summary>
public sealed class JsonFileSaver : IJsonFileSaver
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public string SaveJson(string outputRoot, string tableName, string json)
    {
        Directory.CreateDirectory(outputRoot);

        var fileName = $"{SanitizeFileName(tableName)}.json";
        var outputPath = Path.Combine(outputRoot, fileName);

        File.WriteAllText(outputPath, json, Utf8NoBom);
        return outputPath;
    }

    private static string SanitizeFileName(string name)
    {
        // 시트(탭) 이름의 대소문자를 그대로 유지합니다. (파일 시스템에 못 쓰는 문자만 치환)
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "table";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);

        return sb.ToString();
    }
}
