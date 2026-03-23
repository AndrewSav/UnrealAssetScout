using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Versions;
using UnrealAssetScout.Export;
using UnrealAssetScout.Logging;
using UnrealAssetScout.TypeFiltering;
using Superpower;

namespace UnrealAssetScout.Config;

// Handles CLI argument parsing and CLI-related helper behavior. Used exclusively by Program.Main.
internal static class ConfigOptionsSupport
{
    private const string DocumentationUrl = "https://example.com/unrealassetscout-docs";

    internal static Options? ParseArgs(string[] args)
        => ParseArgsWithExitCode(args).Options;

    internal static ParseArgsResult ParseArgsWithExitCode(string[] args)
    {
        var defaultLogFileName = LogFilePathSupport.GetDefaultLogFileName();
        var rootOptions = CreateRecursiveRootOptions(defaultLogFileName);
        var listOptions = CreateListCommandOptions();
        var exportOptions = CreateExportCommandOptions();

        var root = new RootCommand("Extract or list Unreal Engine pak/utoc assets.")
        {
            rootOptions.Paks,
            rootOptions.Game,
            rootOptions.Aes,
            rootOptions.AesFile,
            rootOptions.Usmap,
            rootOptions.Filter,
            rootOptions.Expression,
            rootOptions.Types,
            rootOptions.MarkUsmap,
            rootOptions.LogCounter,
            rootOptions.Log,
            rootOptions.LogAppend,
            rootOptions.NoLog,
            rootOptions.LogLibraries
        };
        var listCommand = new Command("list", "List files from mounted pak/utoc containers.")
        {
            listOptions.Format,
            listOptions.File
        };
        var exportCommand = new Command("export", "Export files from mounted pak/utoc containers.")
        {
            exportOptions.Mode,
            exportOptions.JsonSkipTypes,
            exportOptions.JsonSkipTypesFile,
            exportOptions.NoSkipTypes,
            exportOptions.ScriptBytecode,
            exportOptions.Output,
            exportOptions.Verbose,
            exportOptions.CompactProgress
        };
        root.Subcommands.Add(listCommand);
        root.Subcommands.Add(exportCommand);
        var versionOption = root.Options.OfType<VersionOption>().Single();
        root.Options.Remove(versionOption);
        ConfigureHelpOption(root);
        var helpAction = root.Options.OfType<HelpOption>().Single().Action;

        var parseResult = root.Parse(args, new ParserConfiguration());
        if (ReferenceEquals(parseResult.Action, helpAction))
        {
            parseResult.Invoke(new InvocationConfiguration
            {
                Output = Console.Error,
                Error = Console.Error
            });
            return new ParseArgsResult(null, 0);
        }

        if (parseResult.Errors.Count > 0)
        {
            parseResult.Invoke(new InvocationConfiguration
            {
                Output = Console.Error,
                Error = Console.Error
            });
            return new ParseArgsResult(null, 1);
        }

        var isExportCommand = ReferenceEquals(parseResult.CommandResult.Command, exportCommand);
        var outputDirectory = isExportCommand ? parseResult.GetRequiredValue(exportOptions.Output) : null;

        var options = new Options
        {
            PaksDirectory = parseResult.GetRequiredValue(rootOptions.Paks).FullName,
            UsmapPath = parseResult.GetValue(rootOptions.Usmap)?.FullName,
            TypeFilterExpression = parseResult.GetValue(rootOptions.Expression),
            TypeFilterCsvPath = parseResult.GetValue(rootOptions.Types)?.FullName,
            OutputDirectory = outputDirectory,
            ListOutputFilePath = !isExportCommand ? parseResult.GetValue(listOptions.File) : null,
            ListFormat = !isExportCommand ? parseResult.GetValue(listOptions.Format) : ListOutputFormat.List,
            Verbose = isExportCommand && parseResult.GetValue(exportOptions.Verbose),
            MarkUsmap = parseResult.GetValue(rootOptions.MarkUsmap),
            CompactProgress = isExportCommand && parseResult.GetValue(exportOptions.CompactProgress),
            ScriptBytecode = isExportCommand && parseResult.GetValue(exportOptions.ScriptBytecode),
            LogCounter = parseResult.GetValue(rootOptions.LogCounter),
            Log = parseResult.GetValue(rootOptions.Log) ?? defaultLogFileName,
            LogSpecified = parseResult.GetResult(rootOptions.Log) is not null,
            LogAppend = parseResult.GetValue(rootOptions.LogAppend),
            NoLog = parseResult.GetValue(rootOptions.NoLog),
            LogLibraries = parseResult.GetValue(rootOptions.LogLibraries),
            Game = parseResult.GetRequiredValue(rootOptions.Game)
        };

        var aesFilePath = parseResult.GetValue(rootOptions.AesFile)?.FullName;
        if (aesFilePath is not null)
        {
            if (!AesKeyConfigSupport.TryReadKeyFile(aesFilePath, out var aesKey))
                return new ParseArgsResult(null, 1);
            options.AesKey = aesKey;
        }
        else
        {
            options.AesKey = parseResult.GetValue(rootOptions.Aes);
        }

        if (isExportCommand)
        {
            options.Mode = parseResult.GetRequiredValue(exportOptions.Mode);
            var inlineSkipTypesSpecified = parseResult.GetResult(exportOptions.JsonSkipTypes) is not null;
            var fileSkipTypesPath = parseResult.GetValue(exportOptions.JsonSkipTypesFile)?.FullName;
            var noSkipTypes = parseResult.GetValue(exportOptions.NoSkipTypes);

            if (noSkipTypes)
            {
                options.JsonSkipTypeNames = [];
            }
            else if (fileSkipTypesPath is not null)
            {
                if (!JsonSkipTypeConfigSupport.TryReadTypeFile(fileSkipTypesPath, out var jsonSkipTypeNames))
                    return new ParseArgsResult(null, 1);
                options.JsonSkipTypeNames = [.. jsonSkipTypeNames];
            }
            else if (inlineSkipTypesSpecified)
            {
                options.JsonSkipTypeNames = [.. parseResult.GetValue(exportOptions.JsonSkipTypes) ?? []];
            }
            else
            {
                options.JsonSkipTypeNames = [.. JsonSkipTypeConfigSupport.DefaultTypeNames];
            }
        }

        if (string.IsNullOrWhiteSpace(options.TypeFilterExpression) != string.IsNullOrWhiteSpace(options.TypeFilterCsvPath))
        {
            AppLog.Error("Type filtering requires both --expression and --types.");
            return new ParseArgsResult(null, 1);
        }

        if (!string.IsNullOrWhiteSpace(options.TypeFilterExpression))
        {
            try
            {
                options.TypeFilterPredicate = new TypeFilterParser().Parse(options.TypeFilterExpression);
            }
            catch (ParseException e)
            {
                AppLog.Error("Invalid type expression for --expression: {Message}", e.Message);
                return new ParseArgsResult(null, 1);
            }
        }

        var filterValue = parseResult.GetValue(rootOptions.Filter);
        if (!string.IsNullOrWhiteSpace(filterValue))
        {
            try
            {
                options.Filter = new Regex(filterValue, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException e)
            {
                AppLog.Error("Invalid regular expression for --filter: {Message}", e.Message);
                return new ParseArgsResult(null, 1);
            }
        }

        return new ParseArgsResult(options, 0);
    }
    private static RootOptions CreateRecursiveRootOptions(string defaultLogFileName)
        => new(
            ConfigOptionFactory.CreateExistingDirectoryOption("--paks", "-p", "Path to the game's Paks folder", required: true, recursive: true),
            ConfigOptionFactory.CreateEnumOption<EGame>("--game", "-g", "Game/engine version from the EGame enum, e.g. GAME_UE5_4", required: true, recursive: true),
            ConfigOptionFactory.CreateStringOption("--aes", "-a", "AES-256 encryption key, e.g. 0xABCD1234...", recursive: true),
            ConfigOptionFactory.CreateExistingFileOption("--aes-file", "-j", "Path to a text file whose first line is the AES-256 key", recursive: true),
            ConfigOptionFactory.CreateExistingFileOption("--usmap", "-u", "Path to a .usmap mappings file", recursive: true),
            ConfigOptionFactory.CreateStringOption("--filter", "-f", "Regular expression; only files whose path matches are processed", recursive: true),
            ConfigOptionFactory.CreateStringOption("--expression", "-e", "Type filter expression; requires --types", recursive: true),
            ConfigOptionFactory.CreateExistingFileOption("--types", "-c", "Path to a list --format types CSV file; requires --expression", recursive: true),
            ConfigOptionFactory.CreateBoolOption("--mark-usmap", "-s", "Prefix files with [*] when usmap is required", recursive: true),
            ConfigOptionFactory.CreateBoolOption("--log-counter", "-r", "Prefix file-associated log lines in the log file with [current/total]", recursive: true),
            ConfigOptionFactory.CreateStringOption("--log", "-l", $"Log file path (default: .\\{defaultLogFileName}; overwritten each run unless --log-append is set)", recursive: true),
            ConfigOptionFactory.CreateBoolOption("--log-append", "-y", "Append to existing log file instead of overwriting each run", recursive: true),
            ConfigOptionFactory.CreateBoolOption("--no-log", "-z", "Disable file logging", recursive: true),
            ConfigOptionFactory.CreateBoolOption("--log-libs", "-b", "Also log CUE4Parse and other dependency warnings/errors", recursive: true));

    private static ListCommandOptions CreateListCommandOptions()
    {
        var format = ConfigOptionFactory.CreateEnumOption<ListOutputFormat>(
            "--format",
            "-t",
            "list: Output format: List, Tree, or Types.");
        format.HelpName = "format";
        var file = new Option<string>("--file", "-o")
        {
            Description = "list: Also write plain list/tree/types output to this file while keeping console output visible."
        };
        file.HelpName = "filename";
        return new(format, file);
    }

    private static ExportCommandOptions CreateExportCommandOptions()
    {
        var output = new Option<string>("--output", "-o")
        {
            Description = "export: Output directory",
            Required = true
        };
        var skipTypes = new Option<string[]>("--skip-types", "-t")
        {
            Description = "export json: Replaces the built-in default skip list with the specified type names",
            HelpName = "types",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var skipTypesFile = ConfigOptionFactory.CreateExistingFileOption("--skip-types-file", "-w", "export json: Path to a text file containing skip type names");
        skipTypesFile.HelpName = "filename";
        var noSkipTypes = ConfigOptionFactory.CreateBoolOption("--no-skip-types", "-k", "export json: Disable the built-in skip list entirely");
        var scriptBytecode = ConfigOptionFactory.CreateBoolOption("--script-bytecode", "-d", "export json: Serialize script bytecode into JSON output. Ignored for other export modes.");

        return new(
            new Argument<ExportMode>("mode")
            {
                Description = "Export mode: Simple, Raw, Json, Textures, Models, Animations, Audio, or Verse"
            },
            skipTypes,
            skipTypesFile,
            noSkipTypes,
            scriptBytecode,
            output,
            ConfigOptionFactory.CreateBoolOption("--verbose", "-v", "export: Print skipped files in the log"),
            ConfigOptionFactory.CreateBoolOption("--compact", "-x", "export: Show compact progress and write full logs to a file"));
    }

    private static void ConfigureHelpOption(RootCommand root)
    {
        var helpOption = root.Options.OfType<HelpOption>().Single();
        helpOption.Action = new DocumentationLinkHelpAction(
            (SynchronousCommandLineAction)helpOption.Action!,
            DocumentationUrl);
    }

    private sealed record RootOptions(
        Option<DirectoryInfo> Paks,
        Option<EGame> Game,
        Option<string> Aes,
        Option<FileInfo> AesFile,
        Option<FileInfo> Usmap,
        Option<string> Filter,
        Option<string> Expression,
        Option<FileInfo> Types,
        Option<bool> MarkUsmap,
        Option<bool> LogCounter,
        Option<string> Log,
        Option<bool> LogAppend,
        Option<bool> NoLog,
        Option<bool> LogLibraries);

    private sealed record ListCommandOptions(
        Option<ListOutputFormat> Format,
        Option<string> File);

    private sealed record ExportCommandOptions(
        Argument<ExportMode> Mode,
        Option<string[]> JsonSkipTypes,
        Option<FileInfo> JsonSkipTypesFile,
        Option<bool> NoSkipTypes,
        Option<bool> ScriptBytecode,
        Option<string> Output,
        Option<bool> Verbose,
        Option<bool> CompactProgress);

    internal sealed record ParseArgsResult(Options? Options, int ExitCode);
}
