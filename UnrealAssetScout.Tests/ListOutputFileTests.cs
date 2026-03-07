namespace UnrealAssetScout.Tests;

[Collection("Logging")]
public class ListOutputFileTests
{
    [Fact]
    public void WriteOutputLine_WritesToConsoleAndOutputFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputFilePath = Path.Combine(tempDir, "list-output.txt");
        var originalOut = Console.Out;
        using var consoleWriter = new StringWriter();

        try
        {
            Console.SetOut(consoleWriter);
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: false,
                fileLoggingEnabled: false,
                logFilePath: string.Empty,
                logLibrariesEnabled: false);

            using var fileWriter = new StreamWriter(outputFilePath);
            ListProcessor.WriteOutputLine("plain-line", fileWriter);
            fileWriter.Flush();
            fileWriter.Dispose();
            RuntimeLogging.CloseAndFlush();

            Assert.Contains("plain-line", consoleWriter.ToString());
            Assert.Equal("plain-line" + Environment.NewLine, File.ReadAllText(outputFilePath));
        }
        finally
        {
            Console.SetOut(originalOut);
            RuntimeLogging.CloseAndFlush();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
