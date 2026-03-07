namespace UnrealAssetScout.Tests;

[Collection("Logging")]
public class ProgramExitCodeTests
{
    [Fact]
    public void Run_WithExplicitHelp_ReturnsZero()
    {
        var exitCode = Program.Run(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Run_WithMissingResponseFile_ReturnsNonZero()
    {
        var missingResponseFile = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"), "missing.rsp");

        var exitCode = Program.Run(["@" + missingResponseFile]);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Run_WithoutCommand_ReturnsNonZero()
    {
        var exitCode = Program.Run([]);

        Assert.NotEqual(0, exitCode);
    }
}
