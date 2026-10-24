using System.Globalization;

namespace EmberHUD.Ui;

public static class Numbers
{
    static readonly string[] Units = ["", "K", "M", "B", "T", "Qa", "Qi"];

    public static string Compact(long value)
    {
        var scaled = (double)value;
        var unit = 0;
        while (scaled >= 999.5 && unit < Units.Length - 1)
        {
            scaled /= 1000;
            unit++;
        }
        var format = unit == 0 ? "0" : scaled < 10 ? "0.00" : scaled < 100 ? "0.0" : "0";
        return scaled.ToString(format, CultureInfo.InvariantCulture) + Units[unit];
    }
}
