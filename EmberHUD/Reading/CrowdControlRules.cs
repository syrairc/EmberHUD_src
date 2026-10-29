using System.Globalization;

namespace EmberHUD.Host.Reading;

internal static class CrowdControlRules
{
    private static readonly Dictionary<int, CrowdControl> Buffs = Parse(Offsets.CcBuffIds);
    private static readonly Dictionary<int, CrowdControl> States = Parse(Offsets.CcStateIds);
    private static readonly Dictionary<int, CrowdControl> Categories = Parse(Offsets.CcCategoryIds);

    public static CrowdControl ForBuff(int id) => Buffs.GetValueOrDefault(id);

    public static CrowdControl ForState(int id) => States.GetValueOrDefault(id);

    public static CrowdControl ForCategory(int id) => Categories.GetValueOrDefault(id);

    private static Dictionary<int, CrowdControl> Parse(string text)
    {
        var map = new Dictionary<int, CrowdControl>();
        foreach (var entry in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('=');
            var flag = Enum.Parse<CrowdControl>(parts[0]);
            foreach (var id in parts[1].Split(','))
            {
                var key = int.Parse(id, CultureInfo.InvariantCulture);
                map[key] = map.GetValueOrDefault(key) | flag;
            }
        }
        return map;
    }
}
