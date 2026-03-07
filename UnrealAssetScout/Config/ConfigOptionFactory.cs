using System;
using System.CommandLine;
using System.IO;

namespace UnrealAssetScout.Config;

// Creates the typed System.CommandLine option instances used by ConfigOptionsSupport.
// Called while building the root and export command shapes so parser-specific construction
// details stay out of the main argument-resolution flow.
internal static class ConfigOptionFactory
{
    internal static Option<DirectoryInfo> CreateExistingDirectoryOption(string name, string alias, string description, bool required = false, bool recursive = false)
    {
        var option = new Option<DirectoryInfo>(name, alias)
        {
            Description = description,
            Required = required,
            Recursive = recursive
        };
        option.AcceptExistingOnly();
        return option;
    }

    internal static Option<FileInfo> CreateExistingFileOption(string name, string alias, string description, bool recursive = false)
    {
        var option = new Option<FileInfo>(name, alias)
        {
            Description = description,
            Recursive = recursive
        };
        option.AcceptExistingOnly();
        return option;
    }

    internal static Option<FileInfo> CreateExistingFileOption(string name, string description, bool recursive = false)
    {
        var option = new Option<FileInfo>(name)
        {
            Description = description,
            Recursive = recursive
        };
        option.AcceptExistingOnly();
        return option;
    }

    internal static Option<TEnum> CreateEnumOption<TEnum>(string name, string alias, string description, bool required = false, bool recursive = false)
        where TEnum : struct, Enum
    {
        var option = new Option<TEnum>(name, alias)
        {
            Description = description,
            HelpName = "game",
            Recursive = recursive,
            Required = required
        };
        return option;
    }

    internal static Option<string> CreateStringOption(string name, string alias, string description, bool recursive = false)
        => new(name, alias) { Description = description, Recursive = recursive };

    internal static Option<bool> CreateBoolOption(string name, string description, bool recursive = false)
        => new(name) { Description = description, Recursive = recursive };

    internal static Option<bool> CreateBoolOption(string name, string alias, string description, bool recursive = false)
        => new(name, alias) { Description = description, Recursive = recursive };
}
