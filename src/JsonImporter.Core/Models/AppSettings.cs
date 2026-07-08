namespace JsonImporter.Core.Models;

/// <summary>
/// 애플리케이션 설정. 테이블 목록과 출력 경로를 담아 설정 파일로 영구 저장됩니다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>JSON 파일이 저장될 루트 디렉토리.</summary>
    public string OutputRoot { get; set; } = "output";

    /// <summary>사용자가 정의한 테이블 목록.</summary>
    public List<DataTableEntry> Tables { get; set; } = new();
}
