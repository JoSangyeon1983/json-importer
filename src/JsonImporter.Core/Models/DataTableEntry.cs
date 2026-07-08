namespace JsonImporter.Core.Models;

/// <summary>
/// 하나의 데이터 테이블 정의. 테이블 이름과 Google Sheets TSV export URL을 담습니다.
/// 원본의 고정 enum(UIPanelOrder, Text ...)을 대체하여, 사용자가 UI/설정 파일에서
/// 자유롭게 테이블을 추가·삭제할 수 있도록 합니다.
/// </summary>
public sealed class DataTableEntry
{
    /// <summary>테이블 이름. 출력 파일명(<c>{name}.json</c>)의 기준이 됩니다.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Google Sheets TSV export URL.</summary>
    public string Url { get; set; } = string.Empty;

    public DataTableEntry() { }

    public DataTableEntry(string name, string url)
    {
        Name = name;
        Url = url;
    }
}
