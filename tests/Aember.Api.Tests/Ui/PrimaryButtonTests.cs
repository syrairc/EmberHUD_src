using System.Collections.Generic;
using Aember.Ui;
using Hexa.NET.ImGui;

namespace Aember.Api.Tests;

public sealed class PrimaryButtonTests
{
    [Fact]
    public void ThePrimaryButtonDrawsWithTheActiveColourAtRest()
    {
        uint gold = 0, plain = 0;
        List<uint> colours = [];
        UiFrame.Run("primary", () =>
        {
            gold = ImGui.GetColorU32(ImGuiCol.ButtonActive);
            plain = ImGui.GetColorU32(ImGuiCol.Button);
            var list = ImGui.GetWindowDrawList();
            var start = list.VtxBuffer.Size;
            Controls.PrimaryButton("Save");
            for (var i = start; i < list.VtxBuffer.Size; i++) colours.Add(list.VtxBuffer[i].Col);
        });
        Assert.NotEqual(gold, plain);
        Assert.Contains(gold, colours);
        Assert.DoesNotContain(plain, colours);
    }
}
