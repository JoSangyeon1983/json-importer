using System.Globalization;
using System.Text;

namespace JsonImporter.Core.Services;

/// <summary>
/// key 기반 테이블(<c>Dictionary&lt;key, Dictionary&lt;column, value&gt;&gt;</c>)을
/// 사람이 읽기 좋은 들여쓰기 JSON 문자열로 직렬화합니다.
/// (원본 <c>DataImporter.JsonSerializer</c> 이식)
/// </summary>
internal static class KeyedJsonSerializer
{
    public static string Serialize(Dictionary<string, Dictionary<string, object>> table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        var entries = new List<KeyValuePair<string, Dictionary<string, object>>>(table);
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            sb.AppendLine($"  \"{EscapeJson(entry.Key)}\": {{");

            var valueEntries = new List<KeyValuePair<string, object>>(entry.Value);
            for (int j = 0; j < valueEntries.Count; j++)
            {
                var kv = valueEntries[j];
                sb.Append($"    \"{EscapeJson(kv.Key)}\": ");
                sb.Append(ValueToJson(kv.Value));
                sb.AppendLine(j < valueEntries.Count - 1 ? "," : string.Empty);
            }

            sb.Append("  }");
            sb.AppendLine(i < entries.Count - 1 ? "," : string.Empty);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ValueToJson(object? value)
    {
        if (value == null)
            return "null";

        if (value is string s)
            return $"\"{EscapeJson(s)}\"";

        if (value is bool b)
            return b ? "true" : "false";

        if (value is int || value is long || value is float || value is double)
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";

        return $"\"{EscapeJson(value.ToString() ?? string.Empty)}\"";
    }

    private static string EscapeJson(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}
