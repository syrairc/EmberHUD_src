using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Tartarus;

static class SettingsUI
{
    public static void DrawPanel(Settings s)
    {
        Setting.Checkbox("Show panel", ref s.ShowPanel, "The advice card next to the screen's choice");
        Setting.Slider("Panel X", ref s.PanelX, 0, 1, "Left edge, as a fraction of the screen width");
        Setting.Slider("Panel Y", ref s.PanelY, 0, 1, "Top edge, as a fraction of the screen height");
    }

    public static void DrawAdvice(Settings s)
    {
        ImGui.SeparatorText("Tier values");
        Number("Tier base", ref s.TierBase, 1, 10, "An inmate of tier t is worth base^t");
        Number("Rainbow multiplier", ref s.RainbowMult, 1, 5, "Extra worth of the jump to the top tier");
        Number("Chain upside", ref s.ChainUpside, 0, 3, "How much a good consume is expected to chain on");
        Setting.Slider("Chromatic cap", ref s.ChromaticCap, 1, 30, "Chromatic inmates allowed per run, past it nothing reaches Chromatic");
        ImGui.SeparatorText("Certainty");
        Number("Close margin", ref s.CloseMargin, 0, 1, "Options within this share of the best still count as best");
        Number("Unsure width ratio", ref s.UnsureWidthRatio, 0, 5, "Ranges wider than this times the best value make the panel say unsure");
        Number("Prior low percentile", ref s.PriorLowPercentile, 0, 1, "Low end of the guess for an unseen cell");
        Number("Prior high percentile", ref s.PriorHighPercentile, 0, 1, "High end of the guess for an unseen cell");
        Setting.Slider("Late days", ref s.LateDays, 0, 10, "Ranges shrink in the last days, when little chaining is left");
        Setting.Slider("Standard days", ref s.StandardDays, 1, 60, "Run length the advice is measured for, other lengths read unsure");
        ImGui.SeparatorText("Unseen cell tier odds, tier 0 to 5");
        Odds("Door", s.DoorPrior);
        Odds("Replace", s.ReplacePrior);
    }

    static void Number(string label, ref double value, float min, float max, string tip)
    {
        var shown = (float)value;
        if (Setting.Slider(label, ref shown, min, max, tip)) value = shown;
    }

    static void Odds(string label, double[] prior)
    {
        for (var tier = 0; tier < prior.Length; tier++) Number($"{label} T{tier}", ref prior[tier], 0, 1, "Chance an unseen cell is this tier");
    }
}
