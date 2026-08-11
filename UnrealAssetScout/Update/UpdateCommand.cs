using UnrealAssetScout.Logging;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Update;

// Turns a SelfUpdate run into log output and an exit code.
// Called by ConfigOptionsSupport when the parsed command is `update`.
internal static class UpdateCommand
{
    internal static int Run()
    {
        var result = SelfUpdate.Run();

        switch (result.Outcome)
        {
            case UpdateOutcome.NotPublishedBuild:
                AppLog.Error(
                    "uas {Version} was not installed from a release, so it cannot update itself.",
                    AppVersion.DisplayText);
                return 1;

            case UpdateOutcome.UpToDate:
                AppLog.Information("uas {Version} is already the latest release.", result.Version);
                return 0;

            case UpdateOutcome.Updated:
                AppLog.Information("Updated uas to {Version}.", result.Version);
                return 0;

            case UpdateOutcome.RateLimited:
                AppLog.Error(
                    "GitHub's rate limit for anonymous requests is spent. It resets at {ResetsAt}.",
                    result.RateLimitResetsAt?.ToLocalTime().ToString("t") ?? "an unreported time");
                return 1;

            case UpdateOutcome.Unreachable:
                AppLog.Error("Could not reach the releases endpoint: {Detail}", result.Detail);
                return 1;

            default:
                AppLog.Error("Update failed: {Detail}", result.Detail);
                return 1;
        }
    }
}
