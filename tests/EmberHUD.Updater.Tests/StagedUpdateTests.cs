namespace EmberHUD.Updater.Tests;

public class StagedUpdateTests
{
    static StagedUpdate Staged(TempDir updates)
    {
        updates.File("1.0.5/files/a.txt", "a");
        updates.File("1.0.5/manifest.json", "{}");
        return new StagedUpdate
        {
            Version = "1.0.5",
            FilesDirectory = Path.Combine(updates.Path, "1.0.5", "files"),
            ManifestPath = Path.Combine(updates.Path, "1.0.5", "manifest.json"),
        };
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        using var updates = new TempDir();
        Staged(updates).Save(updates.Path);

        var loaded = StagedUpdate.Load(updates.Path);

        Assert.NotNull(loaded);
        Assert.Equal("1.0.5", loaded.Version);
        Assert.Equal(Path.Combine(updates.Path, "1.0.5", "files"), loaded.FilesDirectory);
        Assert.Equal(Path.Combine(updates.Path, "1.0.5", "manifest.json"), loaded.ManifestPath);
    }

    [Fact]
    public void LoadReturnsNullWhenFoldersAreGone()
    {
        using var updates = new TempDir();
        Staged(updates).Save(updates.Path);
        Directory.Delete(Path.Combine(updates.Path, "1.0.5"), true);

        Assert.Null(StagedUpdate.Load(updates.Path));
    }

    [Fact]
    public void LoadReturnsNullWhenStateIsMissingOrJunk()
    {
        using var updates = new TempDir();
        Assert.Null(StagedUpdate.Load(updates.Path));
        updates.File("state.json", "not json");
        Assert.Null(StagedUpdate.Load(updates.Path));
    }

    [Fact]
    public void ClearRemovesTheState()
    {
        using var updates = new TempDir();
        Staged(updates).Save(updates.Path);
        StagedUpdate.Clear(updates.Path);
        Assert.Null(StagedUpdate.Load(updates.Path));
    }

    [Theory]
    [InlineData("""{"Version":null,"FilesDirectory":null,"ManifestPath":null}""")]
    [InlineData("""{"Version":"1.0.5","FilesDirectory":"","ManifestPath":null}""")]
    [InlineData("null")]
    public void LoadReturnsNullWhenFieldsAreNullOrEmpty(string json)
    {
        using var updates = new TempDir();
        updates.File("state.json", json);
        Assert.Null(StagedUpdate.Load(updates.Path));
    }
}
