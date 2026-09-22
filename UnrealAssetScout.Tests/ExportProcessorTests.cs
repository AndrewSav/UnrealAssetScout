using System.Text;
using CUE4Parse.FileProvider.Objects;
using UnrealAssetScout.Export;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportProcessorTests
{
    [Fact]
    public void ProcessFiles_PathShadowedByAPatchContainer_ExportsOnlyThePatchCopy()
    {
        using var temp = new TempDir();
        var provider = new FakeVfsProvider(StringComparer.OrdinalIgnoreCase);
        provider.Files.AddFiles(new Dictionary<string, GameFile>
        {
            ["Game/Config/A.ini"] = new FakeGameFile("Game/Config/A.ini", Encoding.UTF8.GetBytes("base"))
        }, readOrder: 3);
        provider.Files.AddFiles(new Dictionary<string, GameFile>
        {
            ["Game/Config/A.ini"] = new FakeGameFile("Game/Config/A.ini", Encoding.UTF8.GetBytes("patch"))
        }, readOrder: 103);
        var recorder = new SourceRecorder(temp.Path, UsmapSnapshot.Empty, scriptBytecode: false, isJsonMode: false);

        ExportProcessor.ProcessFiles(
            provider, ExportMode.Raw, temp.Path, filter: null, verbose: false, markUsmap: false,
            compactCounterSink: null, typeFilteredPaths: null, logCounter: false, jsonSkipTypeNames: [],
            recorder: recorder);

        Assert.Equal("patch", File.ReadAllText(Path.Combine(temp.Path, "Game", "Config", "A.ini")));
        Assert.Single(recorder.Records);
    }
}
