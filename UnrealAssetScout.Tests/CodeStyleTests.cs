using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace UnrealAssetScout.Tests;

// Enforces the mechanically checkable rules in CODE_STYLE.md against the source tree itself.
// Runs as part of the normal test suite so a violation fails the build rather than waiting for
// review. The judgement-based rule, whether a comment earns its place, is deliberately not here.
public sealed class CodeStyleTests
{
    private static readonly Regex TypeDeclaration = new(
        @"^\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial|readonly|file|new|unsafe|ref)\s+)*"
        + @"(class|struct|interface|enum|record)\b\s+(?:struct\s+|class\s+)?([A-Za-z_]\w*)",
        RegexOptions.Compiled);

    // Types the one-type-per-file rule exempts because an external framework requires them to be
    // separate: xUnit demands [CollectionDefinition] on its own class.
    private static readonly HashSet<string> ExemptCompanionTypes = ["LoggingCollectionDefinition"];

    // Program is exempt from the header comment rule by convention.
    private static readonly HashSet<string> ExemptFromHeaderComment = ["Program"];

    // Short identifiers CODE_STYLE.md allows: single-letter lambda and loop parameters, file
    // format names, CUE4Parse boundary naming, and the tolerated Dir and stats.
    private static readonly HashSet<string> AllowedShortTokens =
    [
        "e", "i", "j", "g", "n", "s", "v", "x", "lc",
        "svg", "csv", "wem", "bnk", "acb", "awb", "pck", "ini", "png", "json", "toc", "aes",
        "ar", "pkg", "dir", "stats", "args", "id", "max", "min", "ok", "utf"
    ];

    private static readonly Dictionary<string, string> ForbiddenAbbreviations = new()
    {
        ["idx"] = "index", ["ctx"] = "context", ["msg"] = "message", ["opts"] = "options",
        ["cfg"] = "config", ["prev"] = "previous", ["tmp"] = "temporary", ["cnt"] = "count",
        ["val"] = "value", ["obj"] = "object", ["res"] = "result", ["req"] = "request",
        ["resp"] = "response", ["err"] = "error", ["len"] = "length", ["pos"] = "position",
        ["buf"] = "buffer", ["cur"] = "current", ["curr"] = "current", ["attr"] = "attribute",
        ["elem"] = "element", ["param"] = "parameter", ["impl"] = "implementation",
        ["expr"] = "expression", ["src"] = "source", ["dst"] = "destination",
        ["dest"] = "destination", ["prop"] = "property", ["dict"] = "dictionary",
        ["seq"] = "sequence", ["arr"] = "array", ["lst"] = "list", ["str"] = "string",
        ["num"] = "number", ["desc"] = "description", ["fn"] = "function"
    };

    [Fact]
    public void EveryFileDeclaresAtMostOneTopLevelTypeNamedAfterTheFile()
    {
        var failures = new List<string>();

        foreach (var file in SourceFiles())
        {
            var topLevel = TopLevelTypes(File.ReadAllLines(file))
                .Where(type => !ExemptCompanionTypes.Contains(type))
                .ToList();
            if (topLevel.Count == 0)
                continue;

            var expected = Path.GetFileNameWithoutExtension(file);
            if (topLevel.Count > 1)
                failures.Add($"{Relative(file)} declares {topLevel.Count} top-level types: {string.Join(", ", topLevel)}");
            else if (topLevel[0] != expected)
                failures.Add($"{Relative(file)} declares {topLevel[0]}");
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void EveryTopLevelTypeHasAHeaderComment()
    {
        var failures = new List<string>();

        foreach (var file in ProductionSourceFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = TypeDeclaration.Match(lines[i]);
                if (!match.Success || IndentOf(lines[i]) > 0)
                    continue;
                if (ExemptFromHeaderComment.Contains(match.Groups[2].Value))
                    continue;

                var previous = i - 1;
                while (previous >= 0 && (lines[previous].TrimStart().StartsWith('[') || lines[previous].Trim().Length == 0))
                    previous--;

                if (previous < 0 || !lines[previous].TrimStart().StartsWith("//"))
                    failures.Add($"{Relative(file)}:{i + 1} {match.Groups[2].Value} has no header comment");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void NoIdentifierUsesAForbiddenAbbreviation()
    {
        var failures = new List<string>();

        foreach (var file in SourceFiles())
        {
            var source = StripCommentsAndStrings(File.ReadAllText(file));
            foreach (Match match in Regex.Matches(source, @"\b[A-Za-z_][A-Za-z0-9_]*\b"))
            {
                var identifier = match.Value;
                foreach (var token in SplitWords(identifier))
                {
                    if (AllowedShortTokens.Contains(token) || !ForbiddenAbbreviations.TryGetValue(token, out var full))
                        continue;

                    failures.Add($"{Relative(file)}: '{identifier}' uses '{token}', write '{full}'");
                }
            }
        }

        Assert.Empty(failures.Distinct());
    }

    // Only top-level declarations count, and the file-scoped namespaces this project uses put them
    // at indent zero.
    private static List<string> TopLevelTypes(IReadOnlyList<string> lines) =>
        lines.Where(line => IndentOf(line) == 0)
            .Select(line => TypeDeclaration.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[2].Value)
            .ToList();

    private static int IndentOf(string line) => line.Length - line.TrimStart().Length;

    private static IEnumerable<string> SplitWords(string identifier) =>
        Regex.Matches(identifier.TrimStart('_'), @"[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|[a-z0-9]+")
            .Select(match => match.Value.ToLowerInvariant());

    private static string StripCommentsAndStrings(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        source = Regex.Replace(source, @"//.*$", string.Empty, RegexOptions.Multiline);
        source = Regex.Replace(source, "\"\"\".*?\"\"\"", "\"\"", RegexOptions.Singleline);
        source = Regex.Replace(source, @"@""(?:[^""]|"""")*""", "\"\"");
        source = Regex.Replace(source, @"""(?:\\.|[^""\\])*""", "\"\"");

        // Member accesses name someone else's API and cannot be renamed here: CUE4Parse calls
        // them KeyProp, ValueProp and ElementProp. Our own members are still checked, because
        // their declarations remain.
        source = Regex.Replace(source, @"\.\s*[A-Za-z_]\w*", ".");

        // A property pattern names the same members a dotted access would, so it is exempt for
        // the same reason. This also covers named arguments, whose parameter declarations are
        // themselves still checked.
        return Regex.Replace(source, @"([{,]\s*)[A-Za-z_]\w*(?=\s*:)", "$1");
    }

    private static IEnumerable<string> SourceFiles() =>
        new[] { "UnrealAssetScout", "UnrealAssetScout.Tests" }
            .Select(project => Path.Combine(RepositoryRoot, project))
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    // CODE_STYLE.md scopes the header-comment rule to "the UnrealAssetScout project" specifically,
    // unlike the one-type-per-file and naming rules, which the same document states apply to both
    // UnrealAssetScout and UnrealAssetScout.Tests. Test fixtures are not required to carry one.
    private static IEnumerable<string> ProductionSourceFiles() =>
        SourceFiles().Where(file => Relative(file).StartsWith("UnrealAssetScout" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    private static string Relative(string file) => Path.GetRelativePath(RepositoryRoot, file);

    // Located from this file's own compile-time path rather than the test binary's working
    // directory, which moves with the build configuration.
    private static string RepositoryRoot { get; } =
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, ".."));

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
