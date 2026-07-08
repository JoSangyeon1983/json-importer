using JsonImporter.Core.Services;
using Xunit;

namespace JsonImporter.Tests;

public class TsvConverterTests
{
    [Fact]
    public void EmptyInput_ReturnsEmptyObject()
    {
        Assert.Equal("{}", TsvConverter.Convert(string.Empty));
    }

    [Fact]
    public void FirstColumn_BecomesKey_AndIsExcludedFromValue()
    {
        var tsv = "id\tname\nA\tApple";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"A\": {", json);
        Assert.Contains("\"name\": \"Apple\"", json);
        // key 컬럼(id)은 값 객체에 포함되지 않아야 합니다.
        Assert.DoesNotContain("\"id\":", json);
    }

    [Fact]
    public void CommentColumn_IsSkipped()
    {
        var tsv = "id\t//memo\tname\nA\tignore me\tApple";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"name\": \"Apple\"", json);
        Assert.DoesNotContain("memo", json);
        Assert.DoesNotContain("ignore me", json);
    }

    [Fact]
    public void CommentRow_IsSkipped()
    {
        var tsv = "id\tname\n//comment\tskip\nA\tApple";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"A\": {", json);
        Assert.DoesNotContain("comment", json);
        Assert.DoesNotContain("skip", json);
    }

    [Fact]
    public void RowWithEmptyKey_IsSkipped()
    {
        var tsv = "id\tname\n\tOrphan\nA\tApple";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"A\": {", json);
        Assert.DoesNotContain("Orphan", json);
    }

    [Fact]
    public void TypeInference_ProducesUnquotedScalars()
    {
        var tsv = "id\tcount\tratio\tflag\tlabel\nA\t42\t3.5\ttrue\thello";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"count\": 42", json);
        Assert.Contains("\"ratio\": 3.5", json);
        Assert.Contains("\"flag\": true", json);
        Assert.Contains("\"label\": \"hello\"", json);
    }

    [Fact]
    public void DuplicateKey_LastRowWins()
    {
        var tsv = "id\tname\nA\tFirst\nA\tSecond";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("Second", json);
        Assert.DoesNotContain("First", json);
    }
}
