using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace JsonImporter.Core.Services;

/// <summary>
/// <see cref="HttpClient"/> 기반 텍스트 다운로더. UTF-8로 디코딩합니다.
/// 응답 상태·콘텐츠 타입을 검사하여, 비공개 시트가 반환하는 로그인/HTML 페이지를
/// TSV로 오인하지 않고 명확한 오류로 알립니다. 지정한 시간 내에 응답이 없으면 시간 초과 처리합니다.
/// </summary>
public sealed class TextDownloader : ITextDownloader, IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly TimeSpan _timeout;

    public TextDownloader(HttpClient? httpClient = null, TimeSpan? timeout = null)
    {
        if (httpClient is null)
        {
            _http = new HttpClient();
            _ownsClient = true;
        }
        else
        {
            _http = httpClient;
            _ownsClient = false;
        }

        _timeout = timeout ?? DefaultTimeout;

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("JsonImporter");
    }

    public async Task<DownloadResult> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        // 요청별 타임아웃: 호출자의 취소 토큰과 결합합니다.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, HttpCompletionOption.ResponseContentRead, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"다운로드 시간이 초과되었습니다 ({_timeout.TotalSeconds:0}초). URL과 네트워크 연결을 확인하세요.");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"다운로드 실패: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var hint = response.StatusCode is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.NotFound
                    ? " 시트가 '링크가 있는 모든 사용자'에게 공개 상태인지, URL이 올바른지 확인하세요."
                    : string.Empty;

                throw new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).{hint}");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);
            var text = Encoding.UTF8.GetString(bytes);

            // 비공개 시트는 TSV 대신 로그인/안내 HTML 페이지를 200 OK로 반환하는 경우가 많습니다.
            if (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) || LooksLikeHtml(text))
            {
                throw new InvalidDataException(
                    "TSV가 아닌 HTML 페이지가 반환되었습니다. 구글 시트가 '링크가 있는 모든 사용자에게 공개(뷰어)' 상태인지, " +
                    "그리고 export URL이 올바른지 확인하세요.");
            }

            return new DownloadResult(text, ExtractSheetName(response));
        }
    }

    private static bool LooksLikeHtml(string text)
    {
        var head = text.TrimStart();
        return head.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Content-Disposition 헤더(예: <c>"제목 - 탭이름.tsv"</c>)에서 시트(탭) 이름을 추출합니다.
    /// 헤더가 없거나 파싱할 수 없으면 null을 반환합니다.
    /// </summary>
    private static string? ExtractSheetName(HttpResponseMessage response)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        // FileNameStar(RFC5987, UTF-8 디코딩됨)를 우선 사용, 없으면 FileName.
        var raw = disposition?.FileNameStar ?? disposition?.FileName;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Trim().Trim('"');

        // 확장자(.tsv/.csv 등) 제거
        var dot = raw.LastIndexOf('.');
        if (dot > 0)
            raw = raw[..dot];

        // "제목 - 탭이름" 형식이면 마지막 " - " 뒤(탭 이름)만 사용.
        const string separator = " - ";
        var idx = raw.LastIndexOf(separator, StringComparison.Ordinal);
        var sheet = idx >= 0 ? raw[(idx + separator.Length)..] : raw;

        sheet = sheet.Trim();
        return string.IsNullOrEmpty(sheet) ? null : sheet;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
