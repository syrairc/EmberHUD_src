namespace EmberHUD.Updater.Tests;

public class UpdatePlanTests
{
    static Manifest ManifestOf(params (string Path, string Content)[] files) =>
        new() { Version = "1", Files = files.ToDictionary(f => f.Path, f => Sha(f.Content)) };

    static string Sha(string content)
    {
        using var temp = new TempDir();
        temp.File("x", content);
        return Manifest.Hash(Path.Combine(temp.Path, "x"));
    }

    [Fact]
    public void UnchangedFileIsSkipped()
    {
        using var temp = new TempDir();
        temp.File("EmberHUD.dll", "same");
        var plan = UpdatePlan.Build(temp.Path, null, ManifestOf(("EmberHUD.dll", "same")), false);
        Assert.Empty(plan.Replace);
        Assert.Empty(plan.Remove);
        Assert.Empty(plan.KeptModifiedPlugins);
    }

    [Fact]
    public void NonPluginFileDifferingFromIncomingIsReplacedEvenWhenEdited()
    {
        using var temp = new TempDir();
        temp.File("EmberHUD.dll", "edited");
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("EmberHUD.dll", "old")), ManifestOf(("EmberHUD.dll", "new")), false);
        Assert.Equal(["EmberHUD.dll"], plan.Replace);
    }

    [Fact]
    public void MissingFileIsReplaced()
    {
        using var temp = new TempDir();
        var plan = UpdatePlan.Build(temp.Path, null, ManifestOf(("Plugins/A/Main.cs", "new")), false);
        Assert.Equal(["Plugins/A/Main.cs"], plan.Replace);
    }

    [Fact]
    public void UnmodifiedPluginIsReplaced()
    {
        using var temp = new TempDir();
        temp.File("Plugins/A/Main.cs", "old");
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("Plugins/A/Main.cs", "old")), ManifestOf(("Plugins/A/Main.cs", "new")), false);
        Assert.Equal(["Plugins/A/Main.cs"], plan.Replace);
        Assert.Empty(plan.KeptModifiedPlugins);
    }

    [Fact]
    public void ModifiedPluginIsKeptByDefaultAndListed()
    {
        using var temp = new TempDir();
        temp.File("Plugins/A/Main.cs", "edited");
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("Plugins/A/Main.cs", "old")), ManifestOf(("Plugins/A/Main.cs", "new")), false);
        Assert.Empty(plan.Replace);
        Assert.Equal(["Plugins/A/Main.cs"], plan.KeptModifiedPlugins);
    }

    [Fact]
    public void ModifiedPluginIsReplacedWhenOverwriteIsOn()
    {
        using var temp = new TempDir();
        temp.File("Plugins/A/Main.cs", "edited");
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("Plugins/A/Main.cs", "old")), ManifestOf(("Plugins/A/Main.cs", "new")), true);
        Assert.Equal(["Plugins/A/Main.cs"], plan.Replace);
        Assert.Empty(plan.KeptModifiedPlugins);
    }

    [Fact]
    public void NoInstalledManifestTreatsDifferingPluginsAsModified()
    {
        using var temp = new TempDir();
        temp.File("Plugins/A/Main.cs", "old");
        var plan = UpdatePlan.Build(temp.Path, null, ManifestOf(("Plugins/A/Main.cs", "new")), false);
        Assert.Empty(plan.Replace);
        Assert.Equal(["Plugins/A/Main.cs"], plan.KeptModifiedPlugins);
    }

    [Fact]
    public void NullInstalledHashCountsAsModified()
    {
        using var temp = new TempDir();
        temp.File("Plugins/A/Main.cs", "old");
        var installed = new Manifest { Files = new() { ["Plugins/A/Main.cs"] = null! } };
        var plan = UpdatePlan.Build(temp.Path, installed, ManifestOf(("Plugins/A/Main.cs", "new")), false);
        Assert.Equal(["Plugins/A/Main.cs"], plan.KeptModifiedPlugins);
    }

    [Fact]
    public void ShippedFileMissingFromIncomingIsRemoved()
    {
        using var temp = new TempDir();
        temp.File("Old.dll", "x");
        temp.File("Plugins/A/Main.cs", "same");
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("Old.dll", "x"), ("Plugins/A/Main.cs", "same")), ManifestOf(), false);
        Assert.Equal(["Old.dll", "Plugins/A/Main.cs"], plan.Remove.Order());
        Assert.Empty(plan.KeptModifiedPlugins);
    }

    [Fact]
    public void ModifiedPluginMissingFromIncomingIsKeptNotRemoved()
    {
        using var temp = new TempDir();
        temp.File("Plugins/A/Main.cs", "edited");
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("Plugins/A/Main.cs", "old")), ManifestOf(), false);
        Assert.Empty(plan.Remove);
        Assert.Equal(["Plugins/A/Main.cs"], plan.KeptModifiedPlugins);
    }

    [Fact]
    public void RemovedFileAlreadyGoneFromDiskIsIgnored()
    {
        using var temp = new TempDir();
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("Old.dll", "x")), ManifestOf(), false);
        Assert.Empty(plan.Remove);
    }

    [Fact]
    public void NoInstalledManifestMeansNoRemovals()
    {
        using var temp = new TempDir();
        temp.File("Stray.dll", "x");
        var plan = UpdatePlan.Build(temp.Path, null, ManifestOf(), false);
        Assert.Empty(plan.Remove);
    }

    [Fact]
    public void FilesInNeitherManifestAreNeverTouchedOrHashed()
    {
        using var temp = new TempDir();
        temp.File("Config/x.json", "{}");
        temp.File("EmberHUD.dll", "same");
        using var locked = new FileStream(Path.Combine(temp.Path, "Config", "x.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        var plan = UpdatePlan.Build(temp.Path, ManifestOf(("EmberHUD.dll", "same")), ManifestOf(("EmberHUD.dll", "same")), false);
        Assert.Empty(plan.Replace);
        Assert.Empty(plan.Remove);
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("C:/x")]
    [InlineData("a\\b")]
    public void UnsafeManifestPathThrows(string path)
    {
        using var temp = new TempDir();
        var unsafeManifest = new Manifest { Files = new() { [path] = "abc" } };
        Assert.Throws<InvalidDataException>(() => UpdatePlan.Build(temp.Path, null, unsafeManifest, false));
        Assert.Throws<InvalidDataException>(() => UpdatePlan.Build(temp.Path, unsafeManifest, ManifestOf(), false));
    }
}
