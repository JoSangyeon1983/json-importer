using System.IO;
using System.Text.Json;
using JsonImporter.Core.Models;
using JsonImporter.Core.Services;
using Xunit;

namespace JsonImporter.Tests;

public class ImportServiceTests
{
    /// <summary>다운로드를 대체하는 스텁. 실제 네트워크 없이 파이프라인을 검증합니다.</summary>
    private sealed class FakeDownloader(string payload, string? sheetName = null) : ITextDownloader
    {
        public Task<DownloadResult> DownloadAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(new DownloadResult(payload, sheetName));
    }

    [Fact]
    public async Task Import_WritesValidJsonFile()
    {
        var tsv = "id\tname\tcount\nA\tApple\t3\nB\tBanana\t5";
        var outputRoot = Path.Combine(Path.GetTempPath(), "JsonImporterTests", Guid.NewGuid().ToString("N"));

        try
        {
            var service = new ImportService(new FakeDownloader(tsv), new JsonFileSaver());
            var entry = new DataTableEntry("Fruit", "http://example/tsv");

            var result = await service.ImportAsync(entry, outputRoot);

            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));

            // 저장된 파일이 유효한 JSON이고 기대한 구조인지 확인합니다.
            using var doc = JsonDocument.Parse(File.ReadAllText(result.OutputPath!));
            var apple = doc.RootElement.GetProperty("A");
            Assert.Equal("Apple", apple.GetProperty("name").GetString());
            Assert.Equal(3, apple.GetProperty("count").GetInt32());
        }
        finally
        {
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Import_UsesSheetName_ForOutputFileName()
    {
        var tsv = "id\tname\nA\tApple";
        var outputRoot = Path.Combine(Path.GetTempPath(), "JsonImporterTests", Guid.NewGuid().ToString("N"));

        try
        {
            // 입력한 테이블 이름은 "Table1"이지만, 시트(탭) 이름 "Localization"으로 파일이 저장돼야 합니다.
            var service = new ImportService(new FakeDownloader(tsv, sheetName: "Localization"), new JsonFileSaver());
            var result = await service.ImportAsync(new DataTableEntry("Table1", "http://example/tsv"), outputRoot);

            Assert.True(result.Success, result.Message);
            // 시트 이름의 대소문자가 그대로 유지되어야 합니다.
            Assert.Equal("Localization.json", Path.GetFileName(result.OutputPath));
            Assert.Equal("Localization", result.TableName);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Import_EmptyUrl_Fails()
    {
        var service = new ImportService(new FakeDownloader(""), new JsonFileSaver());
        var result = await service.ImportAsync(new DataTableEntry("T", ""), "unused");

        Assert.False(result.Success);
    }
}
