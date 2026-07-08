using JsonImporter.Core.Services;
using Xunit;

namespace JsonImporter.Tests;

public class SheetUrlNormalizerTests
{
    private const string Id = "1d4ou5K4HF1rwzPhCF3hv4RSKKjdZFj81MLfGpeo9ZGk";
    private const string Export = $"https://docs.google.com/spreadsheets/d/{Id}/export?format=tsv";

    [Fact]
    public void EditUrl_WithGid_BecomesExportUrl()
    {
        var url = $"https://docs.google.com/spreadsheets/d/{Id}/edit?gid=0#gid=0";
        Assert.Equal($"{Export}&gid=0", SheetUrlNormalizer.Normalize(url));
    }

    [Fact]
    public void EditUrl_WithNonZeroGid_PreservesGid()
    {
        var url = $"https://docs.google.com/spreadsheets/d/{Id}/edit#gid=1234567";
        Assert.Equal($"{Export}&gid=1234567", SheetUrlNormalizer.Normalize(url));
    }

    [Fact]
    public void ShareUrl_WithoutGid_OmitsGid()
    {
        // gid가 없으면 Google이 첫 번째 시트를 내보냅니다.
        var url = $"https://docs.google.com/spreadsheets/d/{Id}/edit?usp=sharing";
        Assert.Equal(Export, SheetUrlNormalizer.Normalize(url));
    }

    [Fact]
    public void Fragment_TakesPrecedenceOverQuery()
    {
        var url = $"https://docs.google.com/spreadsheets/d/{Id}/edit?gid=0#gid=999";
        Assert.Equal($"{Export}&gid=999", SheetUrlNormalizer.Normalize(url));
    }

    [Fact]
    public void ExistingExportUrl_IsPassedThrough_WithRangePreserved()
    {
        // 수동으로 붙인 range= 등을 존중해야 합니다.
        var url = $"{Export}&gid=0&range=A5:C12";
        Assert.Equal(url, SheetUrlNormalizer.Normalize(url));
    }

    [Fact]
    public void PublishedToWebUrl_IsPassedThrough()
    {
        // /d/e/<토큰> 형태는 스프레드시트 ID를 담고 있지 않습니다.
        var url = "https://docs.google.com/spreadsheets/d/e/2PACX-1vABC/pubhtml";
        Assert.Equal(url, SheetUrlNormalizer.Normalize(url));
    }

    [Fact]
    public void NonGoogleUrl_IsPassedThrough()
    {
        Assert.Equal("http://example.com/data.tsv", SheetUrlNormalizer.Normalize("http://example.com/data.tsv"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, SheetUrlNormalizer.Normalize(input));
    }

    [Fact]
    public void SurroundingWhitespace_IsTrimmed()
    {
        var url = $"  https://docs.google.com/spreadsheets/d/{Id}/edit#gid=0  ";
        Assert.Equal($"{Export}&gid=0", SheetUrlNormalizer.Normalize(url));
    }
}
