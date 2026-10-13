using System.Drawing;
using Aember;

namespace Market;

public sealed class Settings : PluginSettings
{
    [Menu("Show window", "The listing window, only while the trade results are open")] public ToggleNode Show = new(true);
    [Menu("Current page only", "List only the listings on the page the game shows now")] public ToggleNode CurrentPageOnly = new(false);
    [Menu("Max price", "Hide listings above this price for the whole stack, 0 = off")] public RangeNode<int> MaxPrice = new(0, 0, 1_000_000_000);
    [Menu("Max per unit", "Hide listings above this price per unit, 0 = off")] public RangeNode<float> MaxPerUnit = new(0, 0, 1_000_000);
    [Menu("Stale after (minutes)", "Rows from a page you looked at longer ago than this go grey - other players keep buying")] public RangeNode<int> StaleMinutes = new(5, 1, 60);
    [Menu("Dim non-matches", "Darken every cell on the current page that fails the filters or is sold")] public ToggleNode DimNonMatches = new(true);
    [Menu("Glow colour")] public ColorNode GlowColor = new(Color.FromArgb(0xFF, 0xF9, 0xB6, 0x26));
    [Menu("Glow thickness")] public RangeNode<float> GlowThickness = new(2, 1, 10);
}
