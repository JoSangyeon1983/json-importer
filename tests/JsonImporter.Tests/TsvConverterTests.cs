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

    [Fact]
    public void CommentRowsAboveHeader_AreSkipped()
    {
        var tsv = "//\thttps://docs.google.com/some/url\n//\t\nid\tname\nA\tApple";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"A\": {", json);
        Assert.Contains("\"name\": \"Apple\"", json);
        Assert.DoesNotContain("docs.google.com", json);
    }

    [Fact]
    public void BlankRowsAboveHeader_AreSkipped()
    {
        var tsv = "\t\n\nid\tname\nA\tApple";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"A\": {", json);
        Assert.Contains("\"name\": \"Apple\"", json);
    }

    [Fact]
    public void AllCommentRows_ReturnEmptyObject()
    {
        Assert.Equal("{}", TsvConverter.Convert("//a\tb\n//c\td"));
    }

    [Fact]
    public void EmptyHeaderColumn_DoesNotTruncateLaterColumns()
    {
        // 헤더 중간의 빈 열이 그 뒤 열(ko/en)을 잘라내면 안 됩니다.
        // GetURL.gs가 range를 계산할 때 여기서 멈추던 지점입니다.
        var tsv = "id\t//memo\t\tko\ten\nA\tnote\t\t사과\tApple";
        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"ko\": \"사과\"", json);
        Assert.Contains("\"en\": \"Apple\"", json);
        Assert.DoesNotContain("note", json);
    }

    /// <summary>실제 Localization 시트 레이아웃(주석 행 4개 + 빈 D열)에 대한 회귀 테스트.</summary>
    [Fact]
    public void RealSheetLayout_WithoutRange_ParsesValues()
    {
        var tsv = string.Join("\n",
            "//\thttps://docs.google.com/spreadsheets/d/ID/export?format=tsv&gid=0&range=A5:C12\t\t\t\t",
            "//\t\t\t\t\t",
            "//\t\t\t\t\t",
            "//\t\t\t\t\t",
            "textId\t//코멘트\t//UI 화면\t\tko\ten",
            "// 데모 - 나만의 유람기\t\t\t\t\t",
            "3_3_\t덜미\t시작\t\t불러오는 중입니다\tNow Loading");

        var json = TsvConverter.Convert(tsv);

        Assert.Contains("\"3_3_\": {", json);
        Assert.Contains("\"ko\": \"불러오는 중입니다\"", json);
        Assert.Contains("\"en\": \"Now Loading\"", json);
        // 주석 열과 상단 주석 행의 내용은 결과에 남으면 안 됩니다.
        Assert.DoesNotContain("코멘트", json);
        Assert.DoesNotContain("덜미", json);
        Assert.DoesNotContain("range=", json);
    }
}
