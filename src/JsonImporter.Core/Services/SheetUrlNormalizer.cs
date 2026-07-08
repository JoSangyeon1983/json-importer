using System.Text.RegularExpressions;

namespace JsonImporter.Core.Services;

/// <summary>
/// 사용자가 붙여넣은 Google Sheets URL을 TSV export URL로 정규화합니다.
/// 브라우저 주소창의 <c>/edit?gid=0#gid=0</c>, 공유 버튼의 <c>/edit?usp=sharing</c> 형태를
/// 그대로 받아 <c>/export?format=tsv&amp;gid=0</c> 으로 바꿔줍니다.
/// </summary>
/// <remarks>
/// 다음 경우는 손대지 않고 그대로 통과시킵니다.
/// <list type="bullet">
///   <item>이미 <c>/export</c> 형태인 URL — 수동으로 붙인 <c>range=</c> 등을 보존하기 위함.</item>
///   <item>'웹에 게시'(<c>/spreadsheets/d/e/2PACX-...</c>) URL — export 엔드포인트가 다릅니다.</item>
///   <item>Google Sheets가 아닌 임의의 URL.</item>
/// </list>
/// gid를 찾지 못하면 생략하며, 이 경우 Google은 첫 번째 시트를 내보냅니다.
/// </remarks>
public static class SheetUrlNormalizer
{
    private const string SheetsHost = "docs.google.com/spreadsheets/";
    private const string PublishedPrefix = "/spreadsheets/d/e/";

    private static readonly Regex SpreadsheetIdPattern =
        new(@"/spreadsheets/d/(?<id>[A-Za-z0-9\-_]+)", RegexOptions.Compiled);

    private static readonly Regex GidPattern =
        new(@"[?&#]gid=(?<gid>\d+)", RegexOptions.Compiled);

    /// <summary>URL을 TSV export 형태로 정규화합니다. 대상이 아니면 입력을 그대로(trim만) 돌려줍니다.</summary>
    public static string Normalize(string? url)
    {
        var trimmed = (url ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            return string.Empty;

        if (!trimmed.Contains(SheetsHost, StringComparison.OrdinalIgnoreCase))
            return trimmed;

        // 이미 export URL이면 사용자가 지정한 쿼리(range 등)를 존중합니다.
        if (trimmed.Contains("/export", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        // '웹에 게시' URL은 /d/e/<토큰> 형태라 스프레드시트 ID를 뽑을 수 없습니다.
        if (trimmed.Contains(PublishedPrefix, StringComparison.OrdinalIgnoreCase))
            return trimmed;

        var idMatch = SpreadsheetIdPattern.Match(trimmed);
        if (!idMatch.Success)
            return trimmed;

        var exportUrl = $"https://docs.google.com/spreadsheets/d/{idMatch.Groups["id"].Value}/export?format=tsv";

        var gid = ExtractGid(trimmed);
        return gid is null ? exportUrl : $"{exportUrl}&gid={gid}";
    }

    /// <summary>
    /// gid를 추출합니다. <c>#gid=</c> 프래그먼트를 쿼리 문자열보다 우선합니다
    /// (Google UI가 두 곳에 모두 쓰지만, 시트 탭을 가리키는 정본은 프래그먼트입니다).
    /// </summary>
    private static string? ExtractGid(string url)
    {
        var hashIndex = url.IndexOf('#');
        if (hashIndex >= 0)
        {
            var fragmentMatch = GidPattern.Match(url[hashIndex..]);
            if (fragmentMatch.Success)
                return fragmentMatch.Groups["gid"].Value;
        }

        var match = GidPattern.Match(url);
        return match.Success ? match.Groups["gid"].Value : null;
    }
}
