namespace JsonImporter.Core.Services;

/// <summary>다운로드 결과: 본문 텍스트와, 응답에서 파악한 시트(탭) 이름(있으면).</summary>
/// <param name="Text">다운로드한 TSV 본문.</param>
/// <param name="SheetName">Content-Disposition 헤더에서 추출한 시트(탭) 이름. 없으면 null.</param>
public sealed record DownloadResult(string Text, string? SheetName);
