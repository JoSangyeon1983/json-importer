using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using JsonImporter.Core.Services;
using Xunit;

namespace JsonImporter.Tests;

public class TextDownloaderTests
{
    /// <summary>고정된 응답을 돌려주는 가짜 핸들러. 네트워크 없이 다운로더 로직만 검증합니다.</summary>
    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private static TextDownloader MakeDownloader(HttpResponseMessage response)
        => new(new HttpClient(new StubHandler(response)));

    [Fact]
    public async Task ValidTsv_ReturnsText()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("id\tname\nA\tApple", Encoding.UTF8, "text/tab-separated-values"),
        };

        var result = await MakeDownloader(response).DownloadAsync("http://example/tsv");

        Assert.Contains("Apple", result.Text);
    }

    [Fact]
    public async Task ContentDisposition_ExtractsSheetName()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("id\tname\nA\tApple", Encoding.UTF8, "text/tab-separated-values"),
        };
        // 구글 Sheets export 형식: "제목 - 탭이름.tsv"
        response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = "MyDoc - Text.tsv",
        };

        var result = await MakeDownloader(response).DownloadAsync("http://example/tsv");

        Assert.Equal("Text", result.SheetName);
    }

    [Fact]
    public async Task NoContentDisposition_SheetNameIsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("id\tname\nA\tApple", Encoding.UTF8, "text/tab-separated-values"),
        };

        var result = await MakeDownloader(response).DownloadAsync("http://example/tsv");

        Assert.Null(result.SheetName);
    }

    [Fact]
    public async Task HtmlContentType_ThrowsInvalidData()
    {
        // 비공개 시트가 반환하는 로그인 페이지(HTML) 시나리오.
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Sign in</body></html>", Encoding.UTF8, "text/html"),
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => MakeDownloader(response).DownloadAsync("http://example/private"));
    }

    [Fact]
    public async Task HtmlBodyWithNonHtmlContentType_ThrowsInvalidData()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!DOCTYPE html><html></html>", Encoding.UTF8, "text/plain"),
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => MakeDownloader(response).DownloadAsync("http://example/private"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ErrorStatus_ThrowsHttpRequest(HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(string.Empty),
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => MakeDownloader(response).DownloadAsync("http://example/denied"));
    }
}
