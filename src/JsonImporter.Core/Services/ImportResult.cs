namespace JsonImporter.Core.Services;

/// <summary>단일 테이블 import 시도의 결과.</summary>
/// <param name="Success">성공 여부.</param>
/// <param name="Message">사용자 표시용 메시지.</param>
/// <param name="OutputPath">저장된 JSON 파일 경로(성공 시).</param>
/// <param name="TableName">출력에 사용된 이름(시트 탭 이름 등). UI의 테이블 이름 자동 채움에 사용.</param>
public sealed record ImportResult(bool Success, string Message, string? OutputPath = null, string? TableName = null)
{
    public static ImportResult Ok(string message, string outputPath, string? tableName = null)
        => new(true, message, outputPath, tableName);

    public static ImportResult Failure(string message) => new(false, message);
}
