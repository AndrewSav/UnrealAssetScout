using UnrealAssetScout.Config;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Tests;

[Collection("Logging")]
public class VersionOutputTests
{
    [Fact]
    public void Version_IsWrittenToStandardOutput()
    {
        // install.ps1 captures `uas --version` to report what it installed, and standard error
        // cannot be captured portably across Windows PowerShell 5.1 and PowerShell 7.
        var (standardOutput, standardError, exitCode) = Capture(["--version"]);

        Assert.Equal(0, exitCode);
        Assert.Contains(AppVersion.VersionText, standardOutput);
        Assert.Empty(standardError.Trim());
    }

    [Fact]
    public void Help_IsWrittenToStandardOutput()
    {
        var (standardOutput, standardError, exitCode) = Capture(["--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", standardOutput);
        Assert.Empty(standardError.Trim());
    }

    [Fact]
    public void UsageAfterABadCommandLine_StaysOnStandardError()
    {
        var (standardOutput, standardError, exitCode) = Capture(["--bogus"]);

        Assert.NotEqual(0, exitCode);
        Assert.Empty(standardOutput.Trim());
        Assert.Contains("Usage:", standardError);
    }

    private static (string StandardOutput, string StandardError, int ExitCode) Capture(string[] args)
    {
        var originalOutput = Console.Out;
        var originalError = Console.Error;
        var capturedOutput = new StringWriter();
        var capturedError = new StringWriter();

        try
        {
            Console.SetOut(capturedOutput);
            Console.SetError(capturedError);
            var result = ConfigOptionsSupport.ParseArgsWithExitCode(args);
            return (capturedOutput.ToString(), capturedError.ToString(), result.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }
}
