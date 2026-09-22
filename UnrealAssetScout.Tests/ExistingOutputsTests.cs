using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExistingOutputsTests
{
    [Fact]
    public void Contains_OutputOnDisk_IsTrue()
    {
        using var temp = new TempDir();
        temp.File(Path.Combine("Game", "Content", "A.json"));

        var outputs = new ExistingOutputs(temp.Path);

        Assert.True(outputs.Contains(Path.Combine("Game", "Content", "A.json")));
    }

    [Fact]
    public void Contains_OutputNotOnDisk_IsFalse()
    {
        using var temp = new TempDir();
        temp.File(Path.Combine("Game", "Content", "A.json"));

        var outputs = new ExistingOutputs(temp.Path);

        Assert.False(outputs.Contains(Path.Combine("Game", "Content", "B.json")));
    }

    [Fact]
    public void Contains_NameCasedDifferentlyFromTheFile_AgreesWithFileExists()
    {
        using var temp = new TempDir();
        temp.File(Path.Combine("Game", "A.json"));
        var relative = Path.Combine("GAME", "a.JSON");

        var outputs = new ExistingOutputs(temp.Path);

        Assert.Equal(File.Exists(Path.Combine(temp.Path, relative)), outputs.Contains(relative));
    }

    [Fact]
    public void Contains_FileWrittenAfterTheListing_IsTrue()
    {
        using var temp = new TempDir();
        temp.File("A.json");
        var outputs = new ExistingOutputs(temp.Path);
        Assert.True(outputs.Contains("A.json"));

        temp.File("B.json");

        Assert.True(outputs.Contains("B.json"));
    }

    [Fact]
    public void Contains_OutputDirectoryMissing_IsFalse()
    {
        using var temp = new TempDir();

        var outputs = new ExistingOutputs(Path.Combine(temp.Path, "absent"));

        Assert.False(outputs.Contains("A.json"));
    }
}
