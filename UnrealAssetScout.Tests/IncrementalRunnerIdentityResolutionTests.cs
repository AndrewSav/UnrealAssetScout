using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

// Covers IncrementalRunner.ResolveIdentity, the pure prefix-discrimination-and-parse step behind
// ResolvePackagePath, with fake delegates in place of the provider. Nothing here mounts a game:
// the delegates are the only thing standing in for AbstractVfsFileProvider.FilesById and
// TryGetGameFile, so a regression in the real provider lookups cannot be caught here, only a
// regression in which lookup gets called with what argument.
public sealed class IncrementalRunnerIdentityResolutionTests
{
    private static Func<ulong, string?> Throws(string who) =>
        _ => throw new InvalidOperationException($"{who} should not have been called");

    private static Func<string, string?> ThrowsByName(string who) =>
        _ => throw new InvalidOperationException($"{who} should not have been called");

    [Fact]
    public void ResolveIdentity_PackageIdToken_CallsThePackageIdLookupWithTheParsedId()
    {
        var resolved = IncrementalRunner.ResolveIdentity(
            "packageid:12345",
            resolvePackageId: id => id == 12345UL ? "Game/Dep.uasset" : null,
            resolveGameFilePath: ThrowsByName("resolveGameFilePath"));

        Assert.Equal("Game/Dep.uasset", resolved);
    }

    [Fact]
    public void ResolveIdentity_SlashPrefixedName_CallsTheGameFileLookupWithTheWholeIdentity()
    {
        var resolved = IncrementalRunner.ResolveIdentity(
            "/Game/Dep",
            resolvePackageId: Throws("resolvePackageId"),
            resolveGameFilePath: name => name == "/Game/Dep" ? "Game/Dep.uasset" : null);

        Assert.Equal("Game/Dep.uasset", resolved);
    }

    [Fact]
    public void ResolveIdentity_MalformedPackageIdToken_ReturnsNullWithoutCallingEitherLookup()
    {
        var resolved = IncrementalRunner.ResolveIdentity(
            "packageid:not-a-number",
            resolvePackageId: Throws("resolvePackageId"),
            resolveGameFilePath: ThrowsByName("resolveGameFilePath"));

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveIdentity_PackageIdThatDoesNotResolve_ReturnsNull()
    {
        var resolved = IncrementalRunner.ResolveIdentity(
            "packageid:999",
            resolvePackageId: _ => null,
            resolveGameFilePath: ThrowsByName("resolveGameFilePath"));

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveIdentity_NameThatDoesNotResolve_ReturnsNull()
    {
        var resolved = IncrementalRunner.ResolveIdentity(
            "/Game/Missing",
            resolvePackageId: Throws("resolvePackageId"),
            resolveGameFilePath: _ => null);

        Assert.Null(resolved);
    }
}
