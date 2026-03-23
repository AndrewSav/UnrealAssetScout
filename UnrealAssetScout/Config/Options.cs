using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Versions;
using UnrealAssetScout.Export;
using UnrealAssetScout.TypeFiltering;

namespace UnrealAssetScout.Config;

// Data class holding all resolved CLI and response-file options for a single run.
// Populated by ConfigOptionsSupport.ParseArgs and consumed throughout Program.Main.
internal class Options
{
    public string? PaksDirectory { get; set; }
    public string? AesKey { get; set; }
    public string? UsmapPath { get; set; }
    public string? TypeFilterExpression { get; set; }
    public string? TypeFilterCsvPath { get; set; }
    public Func<PackageModel, bool>? TypeFilterPredicate { get; set; }
    public EGame? Game { get; set; }
    public ExportMode? Mode { get; set; }
    public string? OutputDirectory { get; set; }
    public string? ListOutputFilePath { get; set; }
    public Regex? Filter { get; set; }
    public ListOutputFormat ListFormat { get; set; }
    public bool Verbose { get; set; }
    public bool MarkUsmap { get; set; }
    public bool CompactProgress { get; set; }
    public bool LogCounter { get; set; }
    public string Log { get; set; } = string.Empty;
    public bool LogSpecified { get; set; }
    public bool LogAppend { get; set; }
    public bool NoLog { get; set; }
    public bool LogLibraries { get; set; }
    public bool ScriptBytecode { get; set; }
    public List<string> JsonSkipTypeNames { get; set; } = [];
}
