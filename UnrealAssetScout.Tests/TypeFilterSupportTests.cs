using UnrealAssetScout.TypeFiltering;
using CsvHelper;

namespace UnrealAssetScout.Tests;

public sealed class TypeFilterSupportTests
{
    [Fact]
    public void LoadPackages_LoadsGroupedPackageModelsFromTypesCsv()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var csvPath = Path.Combine(tempDir, "types.csv");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(csvPath, """
                Path,Type,Count
                Project/Content/A.uasset,UTexture,2
                Project/Content/A.uasset,USoundWave,1
                Project/Content/B.uasset,,
                "Project/Content/Ui,""Hud"".uasset","Type,One",3
                """);

            var packages = TypeFilterSupport.LoadTypeInfo(csvPath);

            Assert.Equal(3, packages.Count);

            var first = Assert.Single(packages, package => package.Path == "Project/Content/A.uasset");
            Assert.Equal(3, first.ExportCount);
            Assert.Equal(2, first.ExportTypeCount);
            Assert.Equal(2, first.TypeCounts["UTexture"]);
            Assert.Equal(1, first.TypeCounts["USoundWave"]);

            var second = Assert.Single(packages, package => package.Path == "Project/Content/B.uasset");
            Assert.Equal(0, second.ExportCount);
            Assert.Equal(0, second.ExportTypeCount);

            var quoted = Assert.Single(packages, package => package.Path == "Project/Content/Ui,\"Hud\".uasset");
            Assert.Equal(3, quoted.ExportCount);
            Assert.Equal(3, quoted.TypeCounts["Type,One"]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryResolveMatchingPaths_FiltersPathsByExpression()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var csvPath = Path.Combine(tempDir, "types.csv");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(csvPath, """
                Path,Type,Count
                Project/Content/A.uasset,UTexture,2
                Project/Content/A.uasset,USoundWave,1
                Project/Content/B.uasset,USoundWave,1
                Project/Content/C.uasset,,
                """);

            var predicate = new TypeFilterParser().Parse("UTexture and %exports > 2");
            var success = TypeFilterSupport.TryGetTypeFilteredPaths(predicate, csvPath, out var matchingPaths);

            Assert.True(success);
            Assert.NotNull(matchingPaths);
            Assert.Equal(["Project/Content/A.uasset"], matchingPaths);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadPackages_InvalidHeader_ThrowsHeaderValidationException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var csvPath = Path.Combine(tempDir, "types.csv");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(csvPath, """
                Path,Type,Total
                Project/Content/A.uasset,UTexture,2
                """);

            Assert.Throws<HeaderValidationException>(() => TypeFilterSupport.LoadTypeInfo(csvPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
