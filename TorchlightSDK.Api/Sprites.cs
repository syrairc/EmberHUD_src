using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK;

public sealed record IconAtlas(IReadOnlyDictionary<string, Sprite> Sprites);

public readonly record struct Sprite(ImTextureRef Texture, Vector2 Uv0, Vector2 Uv1);
