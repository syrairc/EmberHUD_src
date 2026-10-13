using System.Drawing;
using Aember;
using Aember.Ui;

namespace MapInfoPlugin;

public sealed class GeneralTab
{
    public ToggleNode Show = new(true);
    [Menu("Lock", "A locked panel ignores the mouse - hold the move key to drag or resize it anyway")] public ToggleNode Lock = new(true);
    [Menu("Move key", "Hold to move or resize a locked panel")] public HotkeyNode MoveKey = new(new Hotkey(Key.Alt));
    [Menu("Hide in town", "Nothing draws in town or the hideout")] public ToggleNode HideInTown = new(true);
    [Menu("Hide behind full-screen panels", "Nothing draws while the game has a full-screen panel open, e.g. the big map")] public ToggleNode HideBehindPanels = new(true);
    public RangeNode<int> X = new(40, 0, 7680);
    public RangeNode<int> Y = new(220, 0, 4320);
    public RangeNode<int> Width = new(360, 160, 1600);
}

public sealed class SectionsTab
{
    [Menu("Map name")] public ToggleNode Title = new(true);
    [Menu("Monster level")] public ToggleNode Level = new(true);
    [Menu("Remaining enemies")] public ToggleNode Enemies = new(true);
    [Menu("Enemies as progress bar", "Needs Remaining enemies on - replaces the enemy count with a bar that fills as enemies die, text is remaining / the highest count seen this map")] public ToggleNode EnemiesBar = new(false);
    [Menu("XP gain", "Only shows when the game's value could be read")] public ToggleNode Xp = new(true);
    [Menu("Content", "Vigil, tarot, gods, etc with a done / in progress / not done mark")] public ToggleNode Content = new(true);
    [Menu("Modifiers", "Every map modifier with text that isn't already a content row")] public ToggleNode Modifiers = new(true);
}

public sealed class LookTab
{
    [Menu("Map name")] public TextStyle Title = new() { Face = FontFace.Bold, Scale = 1.25f, ShadowEnabled = true };
    [Menu("Level / enemies / XP")] public TextStyle Lines = TextStyle.Hud();
    [Menu("Content")] public TextStyle ContentText = new() { ShadowEnabled = true };
    [Menu("Modifiers")] public TextStyle Modifiers = TextStyle.Hud();
    [Menu("Enemies bar text")] public TextStyle Bar = TextStyle.Hud();
    [Menu("Enemies bar fill")] public ColorNode BarFill = new(Color.FromArgb(0xFF, 0xF9, 0xB6, 0x26));
    [Menu("Enemies bar track")] public ColorNode BarTrack = new(Color.FromArgb(0x80, 0, 0, 0));
    [Menu("Enemies bar height")] public RangeNode<float> BarHeight = new(18, 8, 40);
    [Menu("Underline", "Gold line under the map name, fades out to the right")] public ColorNode Underline = new(Color.FromArgb(0xFF, 0x76, 0x57, 0x18));
    [Menu("Icon size")] public RangeNode<float> IconSize = new(20, 10, 48);
    [Menu("Done")] public ColorNode Done = new(Color.FromArgb(0xFF, 0x6C, 0xD9, 0x6C));
    [Menu("In progress")] public ColorNode Active = new(Color.FromArgb(0xFF, 0xF9, 0xB6, 0x26));
    [Menu("Not done")] public ColorNode Pending = new(Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));
}

public sealed class Settings : PluginSettings
{
    public GeneralTab General = new();
    public SectionsTab Sections = new();
    public LookTab Look = new();

    public override void Draw() => Windows.TabBar("mapinfo",
        ("General", () => SettingsDrawer.DrawNodes(General)),
        ("Sections", () => SettingsDrawer.DrawNodes(Sections)),
        ("Look", () => SettingsDrawer.DrawNodes(Look)));
}
