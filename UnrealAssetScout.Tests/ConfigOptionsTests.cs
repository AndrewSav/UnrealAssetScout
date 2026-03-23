using UnrealAssetScout.Export;
using UnrealAssetScout.Export.Exporters;

namespace UnrealAssetScout.Tests;

[Collection("Logging")]
public class ConfigOptionsTests
{
    [Fact]
    public void ParseArgs_ExplicitResponseFiles_ChainAddsFlagsWithoutRepeatingScalars()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var out1 = Path.Combine(tempDir, "out1");
        var cfg1 = Path.Combine(tempDir, "first.rsp");
        var cfg2 = Path.Combine(tempDir, "second.rsp");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            File.WriteAllText(cfg1, $$"""
                export json
                --paks "{{paksDir}}"
                --game GAME_UE5_3
                --output "{{out1}}"
                """);

            File.WriteAllText(cfg2, $$"""
                --verbose
                """);

            var options = ConfigOptionsSupport.ParseArgs(
            [
                "@" + cfg1,
                "@" + cfg2
            ]);

            Assert.NotNull(options);
            Assert.Equal(paksDir, options.PaksDirectory);
            Assert.Equal(ExportMode.Json, options.Mode);
            Assert.Equal(out1, options.OutputDirectory);
            Assert.True(options.Verbose);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_NestedResponseFiles_AreLoaded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var outerFile = Path.Combine(tempDir, "outer.rsp");
        var innerFile = Path.Combine(tempDir, "inner.rsp");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            File.WriteAllText(innerFile, $$"""
                list
                --paks "{{paksDir}}"
                --game GAME_UE5_3
                """);

            File.WriteAllText(outerFile, $$"""
                @{{innerFile}}
                """);

            var options = ConfigOptionsSupport.ParseArgs(["@" + outerFile]);

            Assert.NotNull(options);
            Assert.Equal(paksDir, options.PaksDirectory);
            Assert.Null(options.Mode);
            Assert.Null(options.OutputDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_RemovedConfigAlias_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var cfgFile = Path.Combine(tempDir, "test.rsp");

        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(cfgFile, "--game GAME_UE5_3");

            var options = ConfigOptionsSupport.ParseArgs(["--config", cfgFile]);

            Assert.Null(options);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_WithRemovedLocalizationMode_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "localization",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--output", Path.Combine(tempDir, "out")
            ]);

            Assert.Null(options);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_RepeatedSharedScalarOption_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "--log", "a.log",
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--log", "b.log"
            ]);

            Assert.Null(options);
            Assert.Contains("--log", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_RepeatedExportScalarOption_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--output", "out1",
                "--output", "out2"
            ]);

            Assert.Null(options);
            Assert.Contains("--output", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_AesDirectValue_SetsAesKey()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", paksDir,
                "--game", "GAME_UE5_3",
                "--aes", "0xDEADBEEF1234"
            ]);

            Assert.NotNull(options);
            Assert.Equal("0xDEADBEEF1234", options.AesKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_AesFile_ReadsKeyFromFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var keyFile = Path.Combine(tempDir, "aes.key");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            File.WriteAllText(keyFile, "  0xDEADBEEF1234  \nsome trailing content\n");

            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", paksDir,
                "--game", "GAME_UE5_3",
                "--aes-file", keyFile
            ]);

            Assert.NotNull(options);
            Assert.Equal("0xDEADBEEF1234", options.AesKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_AesFileShortOption_ReadsKeyFromFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var keyFile = Path.Combine(tempDir, "aes.key");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            File.WriteAllText(keyFile, "0xDEADBEEF1234");

            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", paksDir,
                "--game", "GAME_UE5_3",
                "-j", keyFile
            ]);

            Assert.NotNull(options);
            Assert.Equal("0xDEADBEEF1234", options.AesKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_AesFileAtMissingFile_ReturnsNullAndShowsFileValidationError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var missingKeyFile = Path.Combine(tempDir, "nonexistent.key");
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", paksDir,
                "--game", "GAME_UE5_3",
                "--aes-file", missingKeyFile
            ]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains($"File does not exist: '{missingKeyFile}'.", output);
            Assert.Contains("Usage:", output);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_MissingPaksDirectory_ReturnsNullAndShowsDirectoryValidationError()
    {
        var missingPaksDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", missingPaksDir,
                "--game", "GAME_UE5_3"
            ]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains($"Directory does not exist: '{missingPaksDir}'.", output);
            Assert.Contains("Usage:", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ParseArgs_MissingUsmapFile_ReturnsNullAndShowsFileValidationError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var missingUsmapPath = Path.Combine(tempDir, "missing.usmap");
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", paksDir,
                "--game", "GAME_UE5_3",
                "--usmap", missingUsmapPath
            ]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains($"File does not exist: '{missingUsmapPath}'.", output);
            Assert.Contains("Usage:", output);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_JsonSkipTypes_WithExplicitValues_SetsOverrideList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types", "UTexture", "UAnimSequenceBase", "USoundWave",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(["UTexture", "UAnimSequenceBase", "USoundWave"], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_InlineSkipTypes_DoesNotSplitCommaSeparatedValues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types", "UTexture,UAnimSequenceBase", "USoundWave",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(["UTexture,UAnimSequenceBase", "USoundWave"], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_SkipTypesShortOption_SetsOverrideList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "-t", "UTexture", "USoundWave",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(["UTexture", "USoundWave"], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_SkipTypesFileShortOption_LoadsTypeNames()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");
        var typeListPath = Path.Combine(tempDir, "json-skip-types.txt");

        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typeListPath, "UTexture USoundWave");

            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "-w", typeListPath,
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(["UTexture", "USoundWave"], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_NoSkipTypesShortOption_SetsEmptyOverrideList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "-k",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Empty(options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_NoSkipTypes_SetsEmptyOverrideList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--no-skip-types",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Empty(options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_JsonSkipTypesFile_LoadsTypeNames()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");
        var typeListPath = Path.Combine(tempDir, "json-skip-types.txt");

        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typeListPath, """
                UTexture
                USoundWave UAnimSequenceBase
                """);

            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types-file", typeListPath,
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(["UTexture", "USoundWave", "UAnimSequenceBase"], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_JsonSkipTypesFile_LoadsMixedLineCommaAndWhitespaceFormats()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");
        var typeListPath = Path.Combine(tempDir, "json-skip-types.txt");

        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typeListPath, """
                UTexture
                USoundWave UAnimSequenceBase
                UCurveTable,UDataTable
                USkeletalMesh UMaterialInterface
                """);

            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types-file", typeListPath,
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(
            [
                "UTexture",
                "USoundWave",
                "UAnimSequenceBase",
                "UCurveTable",
                "UDataTable",
                "USkeletalMesh",
                "UMaterialInterface"
            ], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_JsonSkipTypesFile_WithMissingFile_ReturnsNullAndShowsFileValidationError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");
        var missingTypeListPath = Path.Combine(tempDir, "missing-types.txt");
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        Directory.CreateDirectory(tempDir);

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types-file", missingTypeListPath,
                "--output", outputDir
            ]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains($"File does not exist: '{missingTypeListPath}'.", output);
            Assert.Contains("Usage:", output);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ResponseFileWithJsonSkipTypesFile_LoadsOneNamePerLineFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var outputDir = Path.Combine(tempDir, "out");
        var configPath = Path.Combine(tempDir, "test.rsp");
        var typeListPath = Path.Combine(tempDir, "json-skip-types.txt");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            File.WriteAllText(typeListPath, """
                UTexture
                USoundWave
                UAnimSequenceBase
                """);

            File.WriteAllText(configPath, $$"""
                export json
                --paks "{{paksDir}}"
                --game GAME_UE5_3
                --skip-types-file "{{typeListPath}}"
                --output "{{outputDir}}"
                """);

            var options = ConfigOptionsSupport.ParseArgs(["@" + configPath]);

            Assert.NotNull(options);
            Assert.Equal(["UTexture", "USoundWave", "UAnimSequenceBase"], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_JsonSkipTypes_WithoutValues_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        Directory.CreateDirectory(tempDir);

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types",
                "--output", outputDir
            ]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("--skip-types", output);
            Assert.Contains("required", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_SkipTypesFile_TakesPrecedenceOverInlineSkipTypes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");
        var typeListPath = Path.Combine(tempDir, "json-skip-types.txt");

        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typeListPath, "UTexture");
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types", "USoundWave",
                "--skip-types-file", typeListPath,
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(["UTexture"], options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_NoSkipTypes_TakesPrecedenceOverOtherSkipTypeOptions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");
        var typeListPath = Path.Combine(tempDir, "json-skip-types.txt");

        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typeListPath, "UTexture");
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--skip-types", "USoundWave",
                "--skip-types-file", typeListPath,
                "--no-skip-types",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Empty(options.JsonSkipTypeNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ResponseFileWithAesFile_ReadsKeyFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var keyFile = Path.Combine(tempDir, "aes.key");
        var cfgFile = Path.Combine(tempDir, "test.rsp");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            File.WriteAllText(keyFile, "0xDEADBEEF1234");
            File.WriteAllText(cfgFile, $$"""
                list
                --paks "{{paksDir}}"
                --game GAME_UE5_3
                --aes-file "{{keyFile}}"
                """);

            var options = ConfigOptionsSupport.ParseArgs(["@" + cfgFile]);

            Assert.NotNull(options);
            Assert.Equal("0xDEADBEEF1234", options.AesKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ScriptBytecode_WithJsonMode_SetsFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--script-bytecode",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.True(options.ScriptBytecode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ScriptBytecode_WithNonJsonMode_IsAccepted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "textures",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--script-bytecode",
                "--output", outputDir
            ]);

            Assert.NotNull(options);
            Assert.Equal(ExportMode.Textures, options.Mode);
            Assert.True(options.ScriptBytecode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_Help_PrintsStockHelpAndDocumentationLink()
    {
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(["--help"]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("Extract or list Unreal Engine pak/utoc assets.", output);
            Assert.Contains("Usage:", output);
            Assert.Contains("Commands:", output);
            Assert.Contains("list", output);
            Assert.Contains("export", output);
            Assert.Contains("--help", output);
            Assert.Contains("Documentation:", output);
            Assert.Contains("https://example.com/unrealassetscout-docs", output);
            Assert.Contains("raw", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("--config <path>", output);
            Assert.DoesNotContain("--no-config", output);
            Assert.DoesNotContain("--help-detailed", output);
            Assert.DoesNotContain("--mode", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ParseArgs_NoCommand_ShowsStockRootHelpAndReturnsNull()
    {
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs([]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Commands:", output);
            Assert.Contains("list", output);
            Assert.Contains("export", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ParseArgs_ListWithoutRequiredOptions_ShowsRequiredErrorsAndReturnsNull()
    {
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(["list"]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("list", output);
            Assert.Contains("Option '--paks' is required.", output);
            Assert.Contains("Option '--game' is required.", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ParseArgs_ListHelp_ShowsShortGamePlaceholderInsteadOfEnumDump()
    {
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(["list", "--help"]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("--game <game>", output);
            Assert.DoesNotContain("GAME_UE5_3|GAME_UE5_4", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ParseArgs_ListDefaultsToListFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3"
            ]);

            Assert.NotNull(options);
            Assert.Equal(ListOutputFormat.List, options.ListFormat);
            Assert.Null(options.Mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ListFormatTree_SetsTreeFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--format", "tree"
            ]);

            Assert.NotNull(options);
            Assert.Equal(ListOutputFormat.Tree, options.ListFormat);
            Assert.Null(options.Mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ListFormatTypes_SetsTypesFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--format", "types"
            ]);

            Assert.NotNull(options);
            Assert.Equal(ListOutputFormat.Types, options.ListFormat);
            Assert.Null(options.Mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ListFile_SetsListOutputFilePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputFile = Path.Combine(tempDir, "list-output.txt");
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--file", outputFile
            ]);

            Assert.NotNull(options);
            Assert.Equal(outputFile, options.ListOutputFilePath);
            Assert.Null(options.Mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ListFileShortOption_SetsListOutputFilePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var outputFile = Path.Combine(tempDir, "list-output.txt");
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "-o", outputFile
            ]);

            Assert.NotNull(options);
            Assert.Equal(outputFile, options.ListOutputFilePath);
            Assert.Null(options.Mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_TypeFilterOptions_SetsExpressionAndTypesPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var typesFile = Path.Combine(tempDir, "types.csv");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typesFile, "Path,Type,Count");
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--expression", "%e > 0",
                "--types", typesFile
            ]);

            Assert.NotNull(options);
            Assert.Equal("%e > 0", options.TypeFilterExpression);
            Assert.Equal(typesFile, options.TypeFilterCsvPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_TypeFilterExpressionWithoutTypes_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--expression", "%e > 0"
            ]);

            Assert.Null(options);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_TypeFilterTypesWithoutExpression_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var typesFile = Path.Combine(tempDir, "types.csv");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typesFile, "Path,Type,Count");
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--types", typesFile
            ]);

            Assert.Null(options);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_InvalidTypeExpression_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var typesFile = Path.Combine(tempDir, "types.csv");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(typesFile, "Path,Type,Count");
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--expression", "UTexture and",
                "--types", typesFile
            ]);

            Assert.Null(options);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ExportFileOption_IsRejected()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        Directory.CreateDirectory(tempDir);

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--output", Path.Combine(tempDir, "out"),
                "--file", Path.Combine(tempDir, "list-output.txt")
            ]);

            Assert.Null(options);
            Assert.Contains("--file", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_LogLibraries_SetsLogLibraries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", tempDir,
                "--game", "GAME_UE5_3",
                "--log-libs"
            ]);

            Assert.NotNull(options);
            Assert.True(options.LogLibraries);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_ExportWithoutRequiredOptions_ShowsRequiredErrorsAndReturnsNull()
    {
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(["export"]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("export", output);
            Assert.Contains("Required argument missing", output);
            Assert.Contains("Option '--output' is required.", output);
            Assert.Contains("Option '--paks' is required.", output);
            Assert.Contains("Option '--game' is required.", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ParseArgs_ExportWithoutOutput_ShowsRequiredErrorAndReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "json",
                "--paks", tempDir,
                "--game", "GAME_UE5_3"
            ]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("export", output);
            Assert.Contains("Option '--output' is required.", output);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_HelpDetailed_IsRejectedAndShowsStandardHelp()
    {
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            var options = ConfigOptionsSupport.ParseArgs(["--help-detailed"]);

            Assert.Null(options);
            var output = errorWriter.ToString();
            Assert.Contains("--help-detailed", output);
            Assert.Contains("Unrecognized command or argument", output);
            Assert.Contains("Usage:", output);
            Assert.Contains("Documentation:", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
