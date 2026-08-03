using UnrealAssetScout.Export;
using UnrealAssetScout.Export.Processors;
using UnrealAssetScout.Incremental;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Tests;

public sealed class PackageModeProcessorBaseTests
{
    private sealed class TestProcessor() : PackageModeProcessorBase(outputDir: "out", verbose: false, modeStats: null)
    {
        internal void SimulateFailure() =>
            LogFailure(
                new PackageExportContext(null, "Game/A.uasset", UsmapRequirement.Unknown, "", PackageLoadResult.Success),
                ExportAttemptResult.Failure("Game/A.uasset", "boom"));
    }

    [Fact]
    public void HasFailure_IsFalseWhenNoFailureWasLogged()
    {
        var processor = new TestProcessor();

        Assert.False(processor.HasFailure);
    }

    [Fact]
    public void HasFailure_BecomesTrueOnceAFailureIsLogged()
    {
        var processor = new TestProcessor();

        processor.SimulateFailure();

        Assert.True(processor.HasFailure);
    }

    [Fact]
    public void ResolveStatus_NoFailure_IsOk()
    {
        var processor = new TestProcessor();

        Assert.Equal(SourceStatus.Ok, ExportProcessor.ResolveStatus(processor));
    }

    [Fact]
    public void ResolveStatus_AnExportFailureAfterASuccessfulLoad_IsFailed()
    {
        // Regression test: a package that loads successfully but whose export attempts all fail
        // must not be recorded as "ok". Before HasFailure existed, ProcessPackageMode only ever saw
        // a package-load failure or an uncaught exception, so this case silently stayed at its
        // initial SourceStatus.Ok even though a [FAILED] line was already logged for it.
        var processor = new TestProcessor();
        processor.SimulateFailure();

        Assert.Equal(SourceStatus.Failed, ExportProcessor.ResolveStatus(processor));
    }
}
