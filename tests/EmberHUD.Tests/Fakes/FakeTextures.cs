using EmberHUD.Host.Icons;
using Hexa.NET.ImGui;

namespace EmberHUD.Tests;

internal sealed class FakeTextures
{
    public Dictionary<string, (int Width, int Height)> Uploaded { get; } = [];

    public List<string> Released { get; } = [];

    public ulong Handle { get; set; } = 1;

    public TextureUpload? UploadOverride { get; set; }

    public bool Upload(string key, byte[] pixels, int width, int height, out string failure)
    {
        if (UploadOverride is not null) return UploadOverride(key, pixels, width, height, out failure);
        Uploaded[key] = (width, height);
        failure = "";
        return true;
    }

    public Sprite? Resolve(string key) => Uploaded.ContainsKey(key) ? new Sprite(new ImTextureRef { TexID = new ImTextureID(Handle) }, Vector2.Zero, Vector2.One) : null;

    public void Release(string key)
    {
        Released.Add(key);
        Uploaded.Remove(key);
    }
}
