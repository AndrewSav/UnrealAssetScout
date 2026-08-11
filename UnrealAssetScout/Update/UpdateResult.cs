using System;

namespace UnrealAssetScout.Update;

// The outcome of one update attempt, with whatever the message for it needs.
// Returned by SelfUpdate.Run and turned into console output and an exit code by UpdateCommand.
internal sealed record UpdateResult(
    UpdateOutcome Outcome,
    string? Version = null,
    DateTimeOffset? RateLimitResetsAt = null,
    string? Detail = null);
