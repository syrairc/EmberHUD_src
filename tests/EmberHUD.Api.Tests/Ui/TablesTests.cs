using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class TablesTests
{
    [Fact]
    public void TablesRowsAndDimsBalance()
    {
        var began = false;
        var rows = 0;
        var columns = 0;
        var alpha = new List<float>();
        UiFrame.Run("tables", () =>
        {
            began = Tables.Begin("table", [("Name", 0), ("Value", 80)]);
            if (!began) return;
            columns = ImGui.TableGetColumnCount();
            using (Tables.Row(0, selected: true))
            {
                rows++;
                alpha.Add(ImGui.GetStyle().Alpha);
            }
            using (Tables.Row(1))
            using (Layout.Dim(true))
            {
                rows++;
                alpha.Add(ImGui.GetStyle().Alpha);
            }
            alpha.Add(ImGui.GetStyle().Alpha);
            Tables.End();
        });
        Assert.True(began);
        Assert.Equal(2, columns);
        Assert.Equal(2, rows);
        Assert.Equal([1f, 0.55f, 1f], alpha.Select(a => MathF.Round(a, 2)));
    }
}
