using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class FilterTests
{
    const string Window = "filter";

    [Fact]
    public void FilterTypesIntoTheStringAndReportsTheChange()
    {
        var filter = "";
        var changed = false;
        var origin = Vector2.Zero;
        void Body()
        {
            origin = ImGui.GetCursorScreenPos();
            changed |= Controls.Filter(ref filter);
        }
        try
        {
            UiFrame.Run(Window, Body);
            UiFrame.Click(Window, origin + new Vector2(20f, UiFrame.Style.FrameHeight / 2), Body);
            UiFrame.Io().AddInputCharactersUTF8("fire");
            UiFrame.Run(Window, Body);
            UiFrame.Run(Window, Body);

            Assert.Equal("fire", filter);
            Assert.True(changed);
        }
        finally { UiFrame.ClickAway(Window, Body); UiFrame.Settle(Window, Body); }
    }
}
