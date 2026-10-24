namespace EmberHUD.Ui;

public enum HAlign { Left, Center, Right }

public sealed class TextLine
{
    public bool Enabled;
    public string Template = "";
    public TextStyle Style = TextStyle.Hud();
    public HAlign Align = HAlign.Center;

    public TextLine Clone()
    {
        var copy = (TextLine)MemberwiseClone();
        copy.Style = Style.Clone();
        return copy;
    }
}
