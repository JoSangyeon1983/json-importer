using System.IO;
using System.Text.Json;
using JsonImporter.Core.Models;

namespace JsonImporter.Core.Services;

/// <summary>
/// <see cref="AppSettings"/>(테이블 목록·출력 경로)를 JSON 파일로 읽고 씁니다.
/// 설정 파일이 없거나 손상된 경우 기본값을 반환합니다.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // 한글 등 비 ASCII 문자를 이스케이프 없이 그대로 저장합니다.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;
    }

    /// <summary>기본 설정 파일 경로: <c>%AppData%/JsonImporter/settings.json</c>.</summary>
    public static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JsonImporter");
        return Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppSettings();

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch
        {
            // 설정 파일이 손상된 경우 기본값으로 복구합니다.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_path, json);
    }
}
