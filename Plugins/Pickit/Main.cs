using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;

namespace Pickit;

public sealed class Main : Plugin<Settings>
{
    readonly Autoloot _autoloot = new();
    readonly TextStyle _numberStyle = new();

    public override void Initialize() => Settings.Normalize();

    public override void Tick()
    {
        if (Settings.NearEnabled || Settings.GemsEnabled) Game.WantGroundItems();
        _autoloot.Step(Settings, Game.State, Input.IsDown, Send.Down, Send.Up, Send.Allowed);
    }

    public override void Render()
    {
        DrawGround();
        DrawInventory();
    }

    void DrawGround()
    {
        if (!Settings.GemsEnabled || Game.State.GroundItems is not { } items || Game.State.Ui.AnyFullScreen) return;
        foreach (var item in items)
        {
            if (item.Legendary) continue;
            if (Settings.DebugRects) EmberHUD.Draw.Rect(item.LabelMin, item.LabelMax, Color.Red);
            foreach (var gem in Gems.Layout(item, Settings)) DrawGem(gem, Settings.GroundTierNumbers, Settings.GroundNumbers);
        }
    }

    void DrawInventory()
    {
        if (!Settings.InventoryEnabled) return;
        var state = Game.State;
        var kind = OpenKind(state.Ui);
        if (kind is null) return;
        Game.WantInventory();
        if (kind == PanelKind.Inventory)
        {
            if (state.Bag is { } bag && state.Inventory is { } inventory)
                DrawCells(inventory, bag.ClipMin, bag.ClipMax, bag.Cells, item => item.Bag == bag.Tab);
        }
        else if (state.Stash is { } stash && state.StashItems is { } items)
            DrawCells(items, stash.ClipMin, stash.ClipMax, stash.Cells, _ => true);
    }

    void DrawCells(IReadOnlyList<InventoryItem> items, Vector2 clipMin, Vector2 clipMax, BagCell[] cells, Func<InventoryItem, bool> onTab)
    {
        using var clip = EmberHUD.Draw.Clip(clipMin, clipMax);
        foreach (var item in items)
        {
            if (!onTab(item) || item.Legendary || item.PrefixIds.Length + item.SuffixIds.Length == 0) continue;
            foreach (var cell in cells)
            {
                if (cell.Slot != item.Slot) continue;
                foreach (var gem in Gems.CellLayout(cell.Min, cell.Max, item.PrefixIds, item.SuffixIds, Settings)) DrawGem(gem, Settings.InventoryTierNumbers, Settings.InventoryNumbers);
                break;
            }
        }
    }

    void DrawGem(Gems.Gem gem, bool tierNumber, TextStyle numbers)
    {
        var style = Settings.Tiers[Math.Clamp(gem.Tier, 0, Settings.Tiers.Count - 1)];
        EmberHUD.Draw.Icon(style.Icon, gem.Centre, gem.Radius, style.Color);
        if (!tierNumber) return;
        var text = Math.Min(gem.Tier, 99).ToString();
        _numberStyle.CopyFrom(numbers);
        _numberStyle.Scale = numbers.Scale * Math.Clamp(gem.Radius / 8, 0.75f, 4);
        EmberHUD.Draw.Text(text, gem.Centre, _numberStyle, Align.Center | Align.Middle);
    }

    static PanelKind? OpenKind(GameUi ui)
    {
        foreach (var panel in ui.Panels)
            if (panel.Kind is PanelKind.Inventory or PanelKind.Stash) return panel.Kind;
        return null;
    }

    public override void OnDisable() => _autoloot.Release(Settings, Send.Up);

    public override void Dispose() => _autoloot.Release(Settings, Send.Up);
}
