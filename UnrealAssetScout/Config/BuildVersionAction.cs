using System.CommandLine;
using System.CommandLine.Invocation;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Config;

// Replaces System.CommandLine's stock version action so that --version reports the same build
// identity as the log header and the incremental manifest. Created by ConfigOptionsSupport when
// configuring the root version option.
internal sealed class BuildVersionAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        parseResult.InvocationConfiguration.Output.WriteLine(AppVersion.DisplayText);
        return 0;
    }
}
