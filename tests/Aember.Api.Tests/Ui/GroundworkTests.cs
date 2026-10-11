using Aember.Ui;
using Hexa.NET.ImGui;

namespace Aember.Api.Tests;

public class GroundworkTests
{
    [Fact]
    public void TrailingWidthIsCachedPerIdStack()
    {
        float right = 0, a = 0, b = 0;
        for (var frame = 0; frame < 3; frame++)
            UiFrame.Run("test", () =>
            {
                right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
                a = Trailing("a", "x");
                b = Trailing("b", "a much longer label");
            });
        Assert.Equal(right, a, 1f);
        Assert.Equal(right, b, 1f);
    }

    static float Trailing(string scope, string button)
    {
        ImGui.PushID(scope);
        ImGui.TextUnformatted(scope);
        Layout.BeginTrailing("##head");
        ImGui.Button(button);
        Layout.EndTrailing();
        ImGui.PopID();
        return ImGui.GetItemRectMax().X;
    }
}
