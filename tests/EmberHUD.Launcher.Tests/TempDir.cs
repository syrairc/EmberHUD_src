namespace EmberHUD.Launcher.Tests;

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("emberhud-launcher-").FullName;

    public string File(string relative, string content = "")
    {
        var path = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, true);
}
