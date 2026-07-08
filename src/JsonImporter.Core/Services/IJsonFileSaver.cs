namespace JsonImporter.Core.Services;

/// <summary>변환된 JSON 문자열을 파일로 저장하는 추상화.</summary>
public interface IJsonFileSaver
{
    /// <summary>JSON을 <c>{outputRoot}/{tableName}.json</c> 로 저장하고 저장 경로를 반환합니다.</summary>
    string SaveJson(string outputRoot, string tableName, string json);
}
