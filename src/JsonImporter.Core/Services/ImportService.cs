using JsonImporter.Core.Models;

namespace JsonImporter.Core.Services;

/// <summary>
/// 하나의 테이블을 import하는 전체 파이프라인을 조율합니다:
/// 다운로드 → TSV→JSON 변환 → 파일 저장.
/// (원본 <c>DataImporter.ImportTable</c>에 해당하며, CSV 저장 단계를 제거했습니다.)
/// </summary>
public sealed class ImportService
{
    private readonly ITextDownloader _downloader;
    private readonly IJsonFileSaver _saver;

    public ImportService(ITextDownloader downloader, IJsonFileSaver saver)
    {
        _downloader = downloader;
        _saver = saver;
    }

    public async Task<ImportResult> ImportAsync(
        DataTableEntry entry,
        string outputRoot,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
            return ImportResult.Failure("테이블 이름이 비어있습니다.");

        if (string.IsNullOrWhiteSpace(entry.Url))
            return ImportResult.Failure($"URL이 비어있습니다: {entry.Name}");

        try
        {
            log?.Invoke($"Downloading: {entry.Name} from {entry.Url}");
            var download = await _downloader.DownloadAsync(entry.Url, cancellationToken).ConfigureAwait(false);
            var tsv = download.Text;

            if (string.IsNullOrWhiteSpace(tsv))
                return ImportResult.Failure($"다운로드한 TSV가 비어있습니다: {entry.Name}");

            log?.Invoke($"Downloaded {tsv.Length} characters for {entry.Name}");

            var json = TsvConverter.Convert(tsv, log);

            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
                return ImportResult.Failure($"변환된 JSON이 비어있습니다: {entry.Name}");

            // 출력 파일 이름은 스프레드시트의 시트(탭) 이름을 사용하고, 없으면 입력한 테이블 이름으로 대체합니다.
            var outputName = !string.IsNullOrWhiteSpace(download.SheetName) ? download.SheetName! : entry.Name;
            if (!string.IsNullOrWhiteSpace(download.SheetName))
                log?.Invoke($"Sheet name from spreadsheet: {download.SheetName}");

            var outputPath = _saver.SaveJson(outputRoot, outputName, json);
            log?.Invoke($"Wrote JSON: {outputPath} ({json.Length} chars)");

            return ImportResult.Ok($"완료: {entry.Name} → {System.IO.Path.GetFileName(outputPath)}", outputPath, outputName);
        }
        catch (OperationCanceledException)
        {
            return ImportResult.Failure($"취소됨: {entry.Name}");
        }
        catch (Exception ex)
        {
            return ImportResult.Failure($"실패: {entry.Name} - {ex.Message}");
        }
    }
}
