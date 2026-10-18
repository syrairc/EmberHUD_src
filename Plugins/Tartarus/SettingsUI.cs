using EmberHUD.Ui;

namespace Tartarus;

static class SettingsUI
{
    public static void DrawPanel(Settings s)
    {
        Setting.Checkbox("Show panel", ref s.ShowPanel, "The advice card next to the screen's choice");
        Setting.Checkbox("Show assumed", ref s.ShowAssumed, "Lists what the advice had to guess, such as tool uses left or events behind unseen cells");
        Setting.Slider("Panel X", ref s.PanelX, 0, 1, "Left edge, as a fraction of the screen width");
        Setting.Slider("Panel Y", ref s.PanelY, 0, 1, "Top edge, as a fraction of the screen height");
    }

    public static void DrawAdvice(Settings s)
    {
        Setting.Slider("Playouts", ref s.Playouts, 20, 2000, "Simulated runs per option, more is steadier but slower to appear");
        var margin = (float)s.CloseMargin;
        if (Setting.Slider("Close margin", ref margin, 0, 1, "Options within this share of the best still count as best")) s.CloseMargin = margin;
    }

    public static void DrawRules(Settings s)
    {
        Setting.Slider("Chromatic cap", ref s.ChromaticCap, 1, 30, "Chromatic inmates allowed per run, past it nothing reaches Chromatic");
        Setting.Checkbox("Eater keeps its beast", ref s.EaterKeepsBeast, "Off: an inmate that eats and rises comes back as a random beast. On: it stays the same beast");
        Setting.Slider("Second meal from tier", ref s.SecondMealFromTier, 0, 5, "Mammoths and lions can eat two beasts down from this tier up");
        Setting.Checkbox("Replace is Execute", ref s.ReplaceIsExecute, "Calls the once-a-day swap Execute on the card, it acts the same either way");
        Setting.Slider("Event chance", ref s.EventChance, 0, 100, "Percent chance that a locked cell hides an event, for the cells you have not seen");
    }
}
