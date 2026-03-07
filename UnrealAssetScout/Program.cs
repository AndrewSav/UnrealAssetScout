using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using UnrealAssetScout.Config;
using UnrealAssetScout.Export;
using UnrealAssetScout.List;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Statistics;
using UnrealAssetScout.TypeFiltering;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout;

public static class Program
{
    public static void Main(string[] args)
    {
        Environment.ExitCode = Run(args);
    }

    internal static int Run(string[] args)
    {
        try
        {
            // This is configuring Serilog for the Command Line parsing output, when we do not know the logging options yet
            RuntimeLogging.ConfigureBootstrapLogger();
            
            var parseArgsResult = ConfigOptionsSupport.ParseArgsWithExitCode(args);
            if (parseArgsResult.Options is null)
                return parseArgsResult.ExitCode;

            var options = parseArgsResult.Options;

            var fileLoggingEnabled = !options.NoLog;
            var logFilePath = LogFilePathSupport.ResolveLogFilePath(options.Log);
            bool compactProgressEnabled = options is { CompactProgress: true, Mode: not null };

            if (fileLoggingEnabled)
                RuntimeLogging.PrepareLogFile(logFilePath, options.LogAppend);

            // This is re-configuring Serilog for the rest of the run, taking into account compact progress mode
            var compactCounterSink = RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled,
                fileLoggingEnabled,
                logFilePath,
                options.LogLibraries);

            if (fileLoggingEnabled)
            {
                if (compactProgressEnabled)
                    Console.Error.WriteLine($"Progress: compact. Mode: {options.Mode!.Value}. Log: {logFilePath}");
                else if (options.LogAppend)
                    AppLog.Information("Appending log to {LogFile}", logFilePath);
                else
                    AppLog.Information("Writing log to {LogFile}", logFilePath);
            }
            else if (compactProgressEnabled)
            {
                Console.Error.WriteLine($"Progress: compact. Mode: {options.Mode!.Value}. Log: disabled (--no-log).");
            }

            var totalStopwatch = Stopwatch.StartNew();
            RunStats? runStats = null;
            var exeDir = AppContext.BaseDirectory;

            // CUE4Parse requires this to download zlib and oodle binaries, otherwise some extractions fail because they are absent
            ZlibHelper.Initialize(Path.Combine(exeDir, ZlibHelper.DLL_NAME));
            OodleHelper.Initialize(Path.Combine(exeDir, OodleHelper.OODLE_NAME_CURRENT));

            var provider = new DefaultFileProvider(
                options.PaksDirectory!,
                SearchOption.TopDirectoryOnly,
                new VersionContainer(options.Game!.Value),
                StringComparer.OrdinalIgnoreCase);

            if (options.UsmapPath is not null)
                provider.MappingsContainer = new FileUsmapTypeMappingsProvider(options.UsmapPath);

            provider.Initialize();

            // Always submit a key for the zero GUID - this is what triggers mounting.
            // For unencrypted containers any key works; for encrypted ones the real key is required.
            FAesKey aesKey;
            try
            {
                aesKey = options.AesKey is not null
                    ? new FAesKey(options.AesKey)
                    : new FAesKey(new byte[32]);
            }
            catch (ArgumentException e)
            {
                AppLog.Error("Invalid AES key value: {Message}", e.Message);
                return 1;
            }

            provider.SubmitKey(new FGuid(), aesKey);

            if (provider.RequiredKeys.Count > 0)
            {
                AppLog.Error(
                    "{Count} container(s) are encrypted and could not be mounted - provide the correct AES key via --aes or --aes-file",
                    provider.RequiredKeys.Count);
                return 1;
            }

            provider.PostMount();
            provider.LoadVirtualPaths();
            RuntimeReporting.WarnIfAesCouldRevealMore(provider, options.AesKey is not null);

            HashSet<string>? typeFilteredPaths = null;
            if (options.TypeFilterPredicate is not null)
            {
                if (!TypeFilterSupport.TryGetTypeFilteredPaths(
                        options.TypeFilterPredicate,
                        options.TypeFilterCsvPath!,
                        out typeFilteredPaths))
                {
                    return 1;
                }
            }

            if (options.MarkUsmap)
                AppLog.Information("Usmap marker enabled: files that require usmap are prefixed with [*].");

            StreamWriter? listOutputWriter = null;
            if (options.Mode is null &&
                !TryCreateListOutputWriter(options.ListOutputFilePath, out listOutputWriter))
            {
                return 1;
            }

            using (listOutputWriter)
            {
                if (options.Mode is null)
                {
                    ListProcessor.ListFiles(provider, options, listOutputWriter, typeFilteredPaths);
                }
                else
                {
                    runStats = ExportProcessor.ProcessFiles(
                        provider,
                        options.Mode.Value,
                        options.OutputDirectory!,
                        options.Filter,
                        options.Verbose,
                        options.MarkUsmap,
                        compactCounterSink,
                        typeFilteredPaths,
                        options.LogCounter,
                        options.JsonSkipTypeNames);
                }
            }

            RuntimeReporting.WriteCompletionSummary(totalStopwatch.Elapsed, runStats, compactProgressEnabled);
            return 0;
        }
        catch (Exception e)
        {
            var exceptionType = e.GetType().FullName ?? e.GetType().Name;
            Console.Error.WriteLine($"Unhandled exception: {exceptionType}: {e.Message}");
            return 1;
        }
        finally
        {
            RuntimeLogging.CloseAndFlush();
        }
    }

    private static bool TryCreateListOutputWriter(string? outputFilePath, out StreamWriter? writer)
    {
        writer = null;
        if (string.IsNullOrWhiteSpace(outputFilePath))
            return true;

        try
        {
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            writer = new StreamWriter(outputFilePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch (Exception e)
        {
            AppLog.Error("Failed to open list output file '{Path}': {Message}", outputFilePath, e.Message);
            return false;
        }
    }
}
