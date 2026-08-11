namespace UnrealAssetScout.Update;

// How an update attempt ended. Produced by SelfUpdate and reported by UpdateCommand.
internal enum UpdateOutcome
{
    // No BuildFlavor, so this build did not come from a release and has nothing to update to.
    NotPublishedBuild,
    UpToDate,
    Updated,
    // GitHub's anonymous hourly budget is spent. Carries the reset time so the message can state it.
    RateLimited,
    Unreachable,
    Failed,
}
