namespace UnrealAssetScout.Tests;

public class FolderTreeRendererTests
{
    [Fact]
    public void RenderFolders_BuildsFolderOnlyAsciiTree()
    {
        var lines = FolderTreeRenderer.RenderFolders(
        [
            "Project/Content/UI/HUD/HUDWidget.uasset",
            "Project/Content/UI/HUD/HUDWidget.uexp",
            "Project/Content/UI/Menu/MenuWidget.uasset",
            "Project/Plugins/Test/Content/Asset.uasset",
            "Engine/Config/Base.ini"
        ]);

        Assert.Equal(
        [
            ".",
            "|-- Engine (0 files)",
            "|   `-- Config (1 file)",
            "`-- Project (0 files)",
            "    |-- Content (0 files)",
            "    |   `-- UI (0 files)",
            "    |       |-- HUD (2 files)",
            "    |       `-- Menu (1 file)",
            "    `-- Plugins (0 files)",
            "        `-- Test (0 files)",
            "            `-- Content (1 file)"
        ], lines);
    }

    [Fact]
    public void RenderFolders_MergesFolderNamesCaseInsensitively()
    {
        var lines = FolderTreeRenderer.RenderFolders(
        [
            "Project/Content/UI/HUD/HUDWidget.uasset",
            "Project/Content/UI/HUD/HUDWidget.uexp",
            "project/content/ui/menu/MenuWidget.uasset"
        ]);

        Assert.Equal(
        [
            ".",
            "`-- Project (0 files)",
            "    `-- Content (0 files)",
            "        `-- UI (0 files)",
            "            |-- HUD (2 files)",
            "            `-- menu (1 file)"
        ], lines);
    }

    [Fact]
    public void RenderFolders_WithOnlyRootFiles_ReturnsNoTree()
    {
        var lines = FolderTreeRenderer.RenderFolders(
        [
            "README.txt",
            "Manifest.json"
        ]);

        Assert.Empty(lines);
    }

    [Fact]
    public void RenderFolders_CountsOnlyImmediateFiles()
    {
        var lines = FolderTreeRenderer.RenderFolders(
        [
            "Project/Content/readme.txt",
            "Project/Content/UI/menu.uasset",
            "Project/Content/UI/HUD/widget.uasset",
            "Project/Content/UI/HUD/widget.uexp"
        ]);

        Assert.Equal(
        [
            ".",
            "`-- Project (0 files)",
            "    `-- Content (1 file)",
            "        `-- UI (1 file)",
            "            `-- HUD (2 files)"
        ], lines);
    }
}
