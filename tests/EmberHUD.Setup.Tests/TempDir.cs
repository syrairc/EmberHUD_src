namespace EmberHUD.Setup.Tests;

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("emberhud-setup-").FullName;

    public string File(string relative, string content = "")
    {
        var path = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public string File(string relative, byte[] content)
    {
        var path = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, true);
}
