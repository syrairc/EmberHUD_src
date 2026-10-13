namespace EmberHUD.Api.Tests;

public sealed class SceneTests : IDisposable
{
    public void Dispose() => Scene.Lister = null;

    [Fact]
    public void NoSourceListsNothing() => Assert.Empty(Scene.Entities());

    [Fact]
    public void EntitiesPassThroughToTheSource()
    {
        SceneEntity[] listed = [new(7, 48, new Vector3(1, 2, 3), true, EntityKind.Monster, "") { Address = 0x1000 }, new(8, 9, null, false, null, "EMapGodOfWar")];
        Scene.Lister = () => listed;

        Assert.Same(listed, Scene.Entities());
        Assert.Equal((nint)0x1000, Scene.Entities()[0].Address);
    }
}
