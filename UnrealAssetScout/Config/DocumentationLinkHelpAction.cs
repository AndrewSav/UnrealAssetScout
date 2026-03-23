using System.CommandLine;
using System.CommandLine.Invocation;
namespace UnrealAssetScout.Config;

// A small wrapper around System.CommandLine's stock help action that appends a documentation
// link. Created by ConfigOptionsSupport when configuring the root help option, then invoked by
// System.CommandLine whenever standard help output is displayed for UnrealAssetScout.
internal sealed class DocumentationLinkHelpAction : SynchronousCommandLineAction
{
    private readonly SynchronousCommandLineAction _innerAction;
    private readonly string _documentationUrl;

    internal DocumentationLinkHelpAction(SynchronousCommandLineAction innerAction, string documentationUrl)
    {
        _innerAction = innerAction;
        _documentationUrl = documentationUrl;
    }

    public override int Invoke(ParseResult parseResult)
    {
        var result = _innerAction.Invoke(parseResult);
        var output = parseResult.InvocationConfiguration.Output;
        output.WriteLine("Documentation:");
        output.WriteLine($"  {_documentationUrl}");
        return result;
    }

    public override bool ClearsParseErrors => _innerAction.ClearsParseErrors;
}
