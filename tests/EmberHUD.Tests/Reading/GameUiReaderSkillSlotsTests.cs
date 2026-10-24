using EmberHUD.Host;

namespace EmberHUD.Tests;

public sealed class GameUiReaderSkillSlotsTests
{
    static readonly Vector2[] Positions = [new(900, 1300), new(1000, 1300), new(1100, 1300), new(1200, 1300), new(1300, 1300)];

    [Fact]
    public void ReadsEverySlotRectInOrder()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddSkillSlots(ui.AddPad([]), Positions, new Vector2(80, 80));

        var slots = world.UiReader().ReadSkillSlots();

        Assert.NotNull(slots);
        Assert.Equal(5, slots.Length);
        for (var i = 0; i < 5; i++) Assert.Equal(Positions[i], slots[i]!.Value.Min);
    }

    [Fact]
    public void HiddenSlotIsNullAndTheRestStay()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var slots = ui.AddSkillSlots(ui.AddPad([]), Positions, new Vector2(80, 80));
        world.Memory.Write(slots[2] + Offsets.UWidgetVisibility, (byte)1);

        var read = world.UiReader().ReadSkillSlots()!;

        Assert.Null(read[2]);
        Assert.NotNull(read[1]);
    }

    [Fact]
    public void NoPadIsNull()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);

        Assert.Null(world.UiReader().ReadSkillSlots());
    }
}
