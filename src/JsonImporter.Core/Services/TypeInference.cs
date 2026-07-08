using System.Globalization;

namespace JsonImporter.Core.Services;

/// <summary>
/// 셀 문자열을 bool / int / float / string 타입으로 추론합니다.
/// (원본 <c>DataImporter.TypeInference</c> 이식)
/// </summary>
internal static class TypeInference
{
    public static object Infer(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        if (bool.TryParse(s, out var b))
            return b;

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;

        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            return f;

        return s;
    }
}
