namespace JsonImporter.Core.Services;

/// <summary>URL로부터 텍스트(TSV)를 내려받는 다운로더 추상화. 테스트 시 대체 가능.</summary>
public interface ITextDownloader
{
    /// <summary>URL에서 TSV를 내려받고, 본문과 시트(탭) 이름을 함께 반환합니다.</summary>
    Task<DownloadResult> DownloadAsync(string url, CancellationToken cancellationToken = default);
}
