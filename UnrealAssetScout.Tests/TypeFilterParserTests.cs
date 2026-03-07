using System.Collections.Generic;
using UnrealAssetScout.TypeFiltering;
using Superpower;

namespace UnrealAssetScout.Tests;

public sealed class TypeFilterParserTests
{
    private static readonly TypeFilterParser Parser = new();

    [Fact]
    public void Parse_MatchesPredicateAgainstTypePresence()
    {
        var filter = Parser.Parse("USoundWave");
        var package = CreatePackage(exportCount: 3, exportTypeCount: 2, typeCounts: new Dictionary<string, int>
        {
            ["UTexture"] = 2,
            ["USoundWave"] = 1
        });

        Assert.True(filter(package));
    }

    [Fact]
    public void Parse_UsesKeywordComparisonsAndUnaryOperators()
    {
        var filter = Parser.Parse("%exports >= 3 and not UTexture");
        var package = CreatePackage(exportCount: 4, exportTypeCount: 1, typeCounts: new Dictionary<string, int>
        {
            ["USoundWave"] = 4
        });

        Assert.True(filter(package));
    }

    [Fact]
    public void Parse_RespectsParenthesesBeforeOr()
    {
        var filter = Parser.Parse("(%exports >= 5 or UTexture) and USoundWave");
        var package = CreatePackage(exportCount: 2, exportTypeCount: 2, typeCounts: new Dictionary<string, int>
        {
            ["UTexture"] = 1
        });

        Assert.False(filter(package));
    }

    [Fact]
    public void Parse_UsesTypeCountsForIdentifierComparisons()
    {
        var filter = Parser.Parse("USoundWave >= 2");
        var package = CreatePackage(exportCount: 3, exportTypeCount: 1, typeCounts: new Dictionary<string, int>
        {
            ["USoundWave"] = 2
        });

        Assert.True(filter(package));
    }

    [Fact]
    public void Parse_AllowsNumberOnLeftSideOfComparison()
    {
        var filter = Parser.Parse("2 < %exports");
        var package = CreatePackage(exportCount: 3, exportTypeCount: 1, typeCounts: new Dictionary<string, int>
        {
            ["USoundWave"] = 3
        });

        Assert.True(filter(package));
    }

    [Fact]
    public void Parse_AllowsKeywordAndTypeReferenceComparisons()
    {
        var filter = Parser.Parse("%types = UTexture");
        var package = CreatePackage(exportCount: 3, exportTypeCount: 2, typeCounts: new Dictionary<string, int>
        {
            ["UTexture"] = 2,
            ["USoundWave"] = 1
        });

        Assert.True(filter(package));
    }

    [Fact]
    public void Parse_AllowsTypeReferenceToTypeReferenceComparisons()
    {
        var filter = Parser.Parse("UTexture > USoundWave");
        var package = CreatePackage(exportCount: 4, exportTypeCount: 2, typeCounts: new Dictionary<string, int>
        {
            ["UTexture"] = 3,
            ["USoundWave"] = 1
        });

        Assert.True(filter(package));
    }

    [Fact]
    public void Parse_ThrowsParseExceptionForIncompleteExpression()
    {
        var exception = Assert.Throws<ParseException>(() => Parser.Parse("USoundWave and"));

        Assert.Contains("Syntax error", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ThrowsHelpfulParseExceptionWhenComparisonValueIsNotANumber()
    {
        var exception = Assert.Throws<ParseException>(() => Parser.Parse("bla > )"));

        Assert.Contains("comparison value", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private static PackageModel CreatePackage(
        int exportCount,
        int exportTypeCount,
        IReadOnlyDictionary<string, int> typeCounts) =>
        new()
        {
            Path = "/Game/TestAsset",
            ExportCount = exportCount,
            ExportTypeCount = exportTypeCount,
            TypeCounts = typeCounts
        };
}
