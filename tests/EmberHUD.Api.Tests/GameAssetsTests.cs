using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class GameAssetsTests : IDisposable
{
    static readonly Sprite Ready = new(new ImTextureRef { TexID = new ImTextureID(7ul) }, Vector2.Zero, Vector2.One);

    long _now;

    public void Dispose()
    {
        TestGame.UseGameAssets(null, null);
        TestGame.UseItems(null, null);
        TestGame.UseSkills(null);
        TestGame.UseBuffs(null);
    }

    void Use(Func<string, Sprite?> resolve, bool extracted = true) => TestGame.UseGameAssets(_ => extracted, (_, name) => resolve(name), () => _now);

    [Fact]
    public void ItemIcon_ResolvesNameThenSprite()
    {
        TestGame.UseItems(id => id == 5 ? new ItemInfo(5, 1, 0, 0, 0, 0, 0, 0, false, "UI_Item_A") : null, null);
        Use(name => name == "UI_Item_A" ? Ready : null);

        Assert.True(Draw.ItemIcon(5, out var sprite));
        Assert.Equal(Ready, sprite);
        Assert.False(Draw.ItemIcon(6, out _));
        Assert.False(Draw.ItemIcon(0, out _));
    }

    [Fact]
    public void ItemIcon_NotExtracted_ReturnsFalse_AndAsksNothingEveryFrame()
    {
        var asked = 0;
        TestGame.UseItems(id =>
        {
            asked++;
            return new ItemInfo(id, 1, 0, 0, 0, 0, 0, 0, false, "UI_Item_A");
        }, null);
        Use(_ => Ready, extracted: false);

        for (var frame = 0; frame < 100; frame++) Assert.False(Draw.ItemIcon(5, out _));

        Assert.Equal(0, asked);
    }

    [Fact]
    public void AMissingNameIsRememberedForThreeSecondsThenAskedAgain()
    {
        var asked = 0;
        TestGame.UseItems(_ =>
        {
            asked++;
            return null;
        }, null);
        Use(_ => Ready);

        for (var frame = 0; frame < 100; frame++) Draw.ItemIcon(5, out _);
        Assert.Equal(1, asked);

        _now += 2999;
        Draw.ItemIcon(5, out _);
        Assert.Equal(1, asked);

        _now += 2;
        Draw.ItemIcon(5, out _);
        Assert.Equal(2, asked);
    }

    [Fact]
    public void AFoundNameIsAskedOnlyOnceAndAnUnreadyTextureCostsNoLookup()
    {
        var asked = 0;
        TestGame.UseSkills(id =>
        {
            asked++;
            return new SkillInfo(id, "UI_Skill_A");
        });
        var ready = false;
        Use(_ => ready ? Ready : null);

        for (var frame = 0; frame < 50; frame++) Assert.False(Draw.SkillIcon(2210, out _));
        ready = true;
        Assert.True(Draw.SkillIcon(2210, out _));
        Assert.Equal(1, asked);
    }

    [Fact]
    public void UniqueIcon_UsesUniqueRowFirst()
    {
        TestGame.UseItems(id => new ItemInfo(id, 1, 0, 0, 0, 0, 0, 0, false, "UI_Base"), id => new ItemInfo(56, 1, 0, 0, 0, 0, 0, 0, true, "UI_Unique" + id));
        Use(name => name == "UI_Unique12" ? Ready : null);

        Assert.True(Draw.UniqueIcon(12, out var sprite));
        Assert.Equal(Ready, sprite);
        Assert.False(Draw.ItemIcon(12, out _));
    }

    [Fact]
    public void BuffIconAsksTheBuffKindAndSkillIconTheSkillKind()
    {
        TestGame.UseBuffs(id => new BuffInfo(id, "UI_Buff"));
        TestGame.UseSkills(id => new SkillInfo(id, "UI_Skill"));
        var kinds = new List<AssetKind>();
        TestGame.UseGameAssets(_ => true, (kind, _) =>
        {
            kinds.Add(kind);
            return Ready;
        }, () => _now);

        Assert.True(Draw.BuffIcon(3, out _));
        Assert.True(Draw.SkillIcon(4, out _));

        Assert.Equal([AssetKind.Buffs, AssetKind.Skills], kinds);
    }

    [Fact]
    public async Task HammeringTheHelpersWhileNamesAreResetStaysConsistent()
    {
        TestGame.UseItems(id => new ItemInfo(id, 1, 0, 0, 0, 0, 0, 0, false, "UI_" + id), id => id % 2 == 0 ? new ItemInfo(1, 1, 0, 0, 0, 0, 0, 0, true, "U_" + id) : null);
        TestGame.UseSkills(id => new SkillInfo(id, "S_" + id));
        TestGame.UseBuffs(id => id % 3 == 0 ? null : new BuffInfo(id, "B_" + id));
        TestGame.UseGameAssets(_ => true, (_, name) => new Sprite(new ImTextureRef { TexID = new ImTextureID((ulong)name.GetHashCode()) }, Vector2.Zero, Vector2.One), () => Environment.TickCount64);
        var stop = false;
        var wrong = 0;
        var workers = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
                for (var id = 1; id < 200; id++)
                {
                    if (Draw.ItemIcon(id, out var s) && s.Texture.TexID.Handle != (ulong)("UI_" + id).GetHashCode()) Interlocked.Increment(ref wrong);
                    Draw.UniqueIcon(id, out _);
                    Draw.SkillIcon(id, out _);
                    Draw.BuffIcon(id, out _);
                }
        }, TestContext.Current.CancellationToken)).ToArray();
        var resetter = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop)) GameAssets.ResetNames();
        }, TestContext.Current.CancellationToken);

        await Task.Delay(300, TestContext.Current.CancellationToken);
        Volatile.Write(ref stop, true);
        await Task.WhenAll([.. workers, resetter]);

        Assert.Equal(0, wrong);
    }

    [Fact]
    public void IsExtracted_AsksTheProbeAndIsFalseWithoutOne()
    {
        Assert.False(GameAssets.IsExtracted(AssetKind.Items));

        TestGame.UseGameAssets(kind => kind == AssetKind.Skills, (_, _) => null);

        Assert.True(GameAssets.IsExtracted(AssetKind.Skills));
        Assert.False(GameAssets.IsExtracted(AssetKind.Items));
    }
}
