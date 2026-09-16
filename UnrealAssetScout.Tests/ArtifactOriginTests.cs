using UnrealAssetScout.Export;

namespace UnrealAssetScout.Tests;

public sealed class ArtifactOriginTests
{
    [Fact]
    public void Key_AnOriginThatIsAWholeContainer_IsJustTheContainerPath()
    {
        var origin = new ArtifactOrigin("Game/WwiseAudio/Media/2641.wem", null);

        Assert.Equal("Game/WwiseAudio/Media/2641.wem", origin.Key);
    }

    [Fact]
    public void Key_AnOriginInsideAContainer_DistinguishesTwoMediaSharingThatContainer()
    {
        var first = new ArtifactOrigin("Game/WwiseAudio/AKB_SE_Weapon.bnk", "Shot (SFX)");
        var second = new ArtifactOrigin("Game/WwiseAudio/AKB_SE_Weapon.bnk", "Reload (SFX)");

        Assert.NotEqual(first.Key, second.Key);
    }
}
