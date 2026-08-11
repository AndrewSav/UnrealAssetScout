using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Update;

// Replaces the running executable with the newest published release, pulled from this repository's
// GitHub releases. Called by UpdateCommand for the `update` subcommand, and for the leftover sweep
// at the start of every run.
internal static class SelfUpdate
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/AndrewSav/UnrealAssetScout/releases/latest";

    // The documented endpoint, rather than the undocumented redirect from the web host. One request
    // per explicit `uas update` is nothing against a 60/hour anonymous budget.
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(15),
    })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private const string ZipMemberName = "uas.exe";

    internal static UpdateResult Run()
    {
        if (!AppVersion.IsPublishedBuild || Environment.ProcessPath is not { } executablePath)
            return new UpdateResult(UpdateOutcome.NotPublishedBuild);

        SweepLeftovers();

        if (CheckWritable(Path.GetDirectoryName(executablePath)!) is { } writeError)
            return new UpdateResult(UpdateOutcome.Failed, Detail: writeError);

        try
        {
            using var response = Http.Send(BuildRequest(LatestReleaseApi));
            if (IsRateLimited(response))
                return new UpdateResult(UpdateOutcome.RateLimited, RateLimitResetsAt: ReadRateLimitReset(response));

            response.EnsureSuccessStatusCode();
            using var release = JsonDocument.Parse(response.Content.ReadAsStream());

            var tag = release.RootElement.GetProperty("tag_name").GetString();
            if (ParseReleaseTag(tag) is not { } published)
                return new UpdateResult(UpdateOutcome.Failed, Detail: $"could not read a version from release tag '{tag}'");

            if (published <= AppVersion.Current)
                return new UpdateResult(UpdateOutcome.UpToDate, AppVersion.VersionText);

            var assetName = AssetNameFor(published, AppVersion.BuildFlavor);
            if (FindAssetUrl(release.RootElement, assetName) is not { } assetUrl)
                return new UpdateResult(UpdateOutcome.Failed, Detail: $"release {tag} does not publish {assetName}");

            var staged = executablePath + ".new";
            Stage(assetUrl, staged);
            SwapExecutable(executablePath, staged);
            return new UpdateResult(UpdateOutcome.Updated, published.ToString(3));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException
                                      or InvalidOperationException or InvalidDataException or IOException)
        {
            return new UpdateResult(UpdateOutcome.Unreachable, Detail: e.Message);
        }
    }

    // A previous update leaves the replaced executable behind, because the process holding it has
    // not exited yet. By the next run it has, so the file is removable.
    internal static void SweepLeftovers()
    {
        if (Environment.ProcessPath is not { } executablePath)
            return;

        TryDelete(executablePath + ".old");
        TryDelete(executablePath + ".new");
    }

    internal static string AssetNameFor(Version version, string? buildFlavor) =>
        buildFlavor == "self-contained"
            ? $"UnrealAssetScout-v{version.ToString(3)}-self-contained.zip"
            : $"UnrealAssetScout-v{version.ToString(3)}.zip";

    internal static Version? ParseReleaseTag(string? tag)
    {
        var trimmed = (tag ?? string.Empty).Trim().TrimStart('v', 'V');
        return trimmed.Count(character => character == '.') == 2 && Version.TryParse(trimmed, out var version)
            ? version
            : null;
    }

    private static string? FindAssetUrl(JsonElement release, string assetName)
    {
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == assetName)
                return asset.GetProperty("browser_download_url").GetString();
        }

        return null;
    }

    private static void Stage(string assetUrl, string staged)
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"uas-update-{Guid.NewGuid():N}.zip");
        try
        {
            // The asset is served from a different host that wants none of the API headers.
            using (var response = Http.Send(
                       new HttpRequestMessage(HttpMethod.Get, assetUrl),
                       HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using var archive = File.Create(archivePath);
                response.Content.ReadAsStream().CopyTo(archive);
            }

            using var zip = ZipFile.OpenRead(archivePath);
            var entry = zip.Entries.FirstOrDefault(candidate =>
                            string.Equals(candidate.Name, ZipMemberName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException($"the downloaded archive does not contain {ZipMemberName}");

            TryDelete(staged);
            entry.ExtractToFile(staged);
        }
        finally
        {
            TryDelete(archivePath);
        }
    }

    // The swap rests on a Windows detail: a running executable cannot be overwritten or deleted, but it
    // can be renamed. The self-extracting single-file build unpacks its native libraries to the temp
    // directory rather than beside the executable, so nothing else holds the file.
    private static void SwapExecutable(string executablePath, string staged)
    {
        var old = executablePath + ".old";
        TryDelete(old);
        File.Move(executablePath, old);
        try
        {
            File.Move(staged, executablePath);
        }
        catch
        {
            File.Move(old, executablePath);
            throw;
        }
    }

    // Fails before downloading rather than after, so a read-only install directory is reported
    // without the wait.
    private static string? CheckWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".uas-update-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"cannot write to {directory}: {e.Message}";
        }
    }

    // A spent budget answers 403 with the remaining count at zero; a 403 for any other reason is
    // not a rate limit.
    private static bool IsRateLimited(HttpResponseMessage response) =>
        response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
        && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
        && remaining.FirstOrDefault() == "0";

    private static DateTimeOffset? ReadRateLimitReset(HttpResponseMessage response) =>
        response.Headers.TryGetValues("X-RateLimit-Reset", out var values)
        && long.TryParse(values.FirstOrDefault(), out var epochSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(epochSeconds)
            : null;

    private static HttpRequestMessage BuildRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        // The API rejects requests without a User-Agent.
        request.Headers.TryAddWithoutValidation("User-Agent", "UnrealAssetScout");
        return request;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
