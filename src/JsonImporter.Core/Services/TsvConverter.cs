using System.IO;

namespace JsonImporter.Core.Services;

/// <summary>
/// TSV → key 기반 JSON 변환기.
/// 첫 번째 유효 컬럼을 key로 사용하고, <c>//</c> 로 시작하는 주석 컬럼/행은 건너뜁니다.
/// (원본 <c>DataImporter.TsvConverter</c>에서 CSV 출력 로직을 제거한 이식본)
/// </summary>
/// <remarks>
/// 헤더는 첫 줄로 고정되지 않고, <c>//</c> 로 시작하지 않는 첫 번째 비어있지 않은 행을 헤더로 봅니다.
/// 덕분에 시트 상단의 주석 행을 <c>range=</c> 지정 없이 건너뛸 수 있습니다.
/// 이 규칙의 전제는 <b>첫 번째 열이 주석 열이 아니라는 것</b>입니다
/// (주석 행 판별과 동일한 전제이며, 첫 열은 key 컬럼으로 쓰입니다).
/// </remarks>
public static class TsvConverter
{
    private const string CommentPrefix = "//";

    /// <summary>TSV 텍스트를 JSON 문자열로 변환합니다. 유효한 데이터가 없으면 <c>"{}"</c>를 반환합니다.</summary>
    /// <param name="tsvText">탭으로 구분된 원본 텍스트.</param>
    /// <param name="log">진행 상황 로그 콜백(선택).</param>
    public static string Convert(string tsvText, Action<string>? log = null)
    {
        var lines = SplitLines(tsvText);
        if (lines.Count == 0)
        {
            log?.Invoke("No lines in TSV");
            return "{}";
        }

        var headerIndex = FindHeaderRowIndex(lines, log);
        if (headerIndex < 0)
        {
            log?.Invoke("No header row in TSV (all rows are comments or blank)");
            return "{}";
        }

        var headerCells = SplitTsvLine(lines[headerIndex]);
        if (headerCells.Count == 0)
        {
            log?.Invoke("No header cells in TSV");
            return "{}";
        }

        log?.Invoke($"Header row: line {headerIndex + 1}");

        log?.Invoke($"Header: {string.Join(" | ", headerCells)}");

        var (includeIndices, includeKeys) = FilterCommentColumns(headerCells, log);

        if (includeKeys.Count < 1)
        {
            log?.Invoke("No valid columns after filtering comments");
            return "{}";
        }

        log?.Invoke($"Using '{includeKeys[0]}' as primary key column");

        var table = ParseDataRows(lines, headerIndex, includeIndices, includeKeys, log);

        log?.Invoke($"Parsed {table.Count} entries");

        return KeyedJsonSerializer.Serialize(table);
    }

    /// <summary>
    /// 헤더 행의 인덱스를 찾습니다: <c>//</c> 주석 행도, 완전히 빈 행도 아닌 첫 번째 행.
    /// 찾지 못하면 -1을 반환합니다.
    /// </summary>
    private static int FindHeaderRowIndex(List<string> lines, Action<string>? log)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var cells = SplitTsvLine(lines[i]);
            var firstCell = cells.Count > 0 ? (cells[0]?.Trim() ?? string.Empty) : string.Empty;

            if (firstCell.StartsWith(CommentPrefix, StringComparison.Ordinal))
            {
                log?.Invoke($"Skipping comment row above header at line {i + 1}");
                continue;
            }

            if (cells.All(string.IsNullOrWhiteSpace))
            {
                log?.Invoke($"Skipping blank row above header at line {i + 1}");
                continue;
            }

            return i;
        }

        return -1;
    }

    private static (List<int> indices, List<string> keys) FilterCommentColumns(
        List<string> headerCells,
        Action<string>? log)
    {
        var indices = new List<int>();
        var keys = new List<string>();

        for (int i = 0; i < headerCells.Count; i++)
        {
            var key = headerCells[i]?.Trim() ?? string.Empty;

            if (key.StartsWith(CommentPrefix, StringComparison.Ordinal))
            {
                log?.Invoke($"Skipping comment column: {key}");
                continue;
            }

            if (string.IsNullOrEmpty(key))
            {
                log?.Invoke($"Skipping empty column at index {i}");
                continue;
            }

            indices.Add(i);
            keys.Add(key);
        }

        log?.Invoke($"Active columns: {string.Join(", ", keys)}");
        return (indices, keys);
    }

    private static Dictionary<string, Dictionary<string, object>> ParseDataRows(
        List<string> lines,
        int headerIndex,
        List<int> includeIndices,
        List<string> includeKeys,
        Action<string>? log)
    {
        var table = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);

        for (int lineIndex = headerIndex + 1; lineIndex < lines.Count; lineIndex++)
        {
            var rowCells = SplitTsvLine(lines[lineIndex]);
            if (rowCells.Count == 0)
                continue;

            var firstCell = rowCells[0]?.Trim() ?? string.Empty;
            if (firstCell.StartsWith(CommentPrefix, StringComparison.Ordinal))
            {
                log?.Invoke($"Skipping comment row at line {lineIndex + 1}");
                continue;
            }

            var keyIndex = includeIndices[0];
            var keyValue = keyIndex < rowCells.Count
                ? (rowCells[keyIndex]?.Trim() ?? string.Empty)
                : string.Empty;

            if (string.IsNullOrEmpty(keyValue))
            {
                log?.Invoke($"Row {lineIndex + 1} has empty key, skipping");
                continue;
            }

            var valueObj = new Dictionary<string, object>(StringComparer.Ordinal);

            // k = 0은 key 컬럼이므로 JSON valueObj 에서는 제외하고, k > 0 컬럼만 값으로 담습니다.
            for (int k = 1; k < includeIndices.Count; k++)
            {
                var cellIndex = includeIndices[k];
                var columnName = includeKeys[k];
                var value = cellIndex < rowCells.Count ? rowCells[cellIndex] : string.Empty;

                valueObj[columnName] = TypeInference.Infer(value);
            }

            if (table.ContainsKey(keyValue))
            {
                log?.Invoke($"Duplicate key '{keyValue}' at row {lineIndex + 1}, overwriting");
            }

            table[keyValue] = valueObj;
        }

        return table;
    }

    private static List<string> SplitLines(string? text)
    {
        var result = new List<string>();
        using var sr = new StringReader(text ?? string.Empty);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            result.Add(line);
        }
        return result;
    }

    private static List<string> SplitTsvLine(string? line)
    {
        return new List<string>((line ?? string.Empty).Split('\t'));
    }
}
