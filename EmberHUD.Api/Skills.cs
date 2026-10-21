namespace EmberHUD;

public readonly record struct SkillInfo(int Id, string? Icon);

public static class Skills
{
    internal static Func<int, SkillInfo?>? Getter { get; set; }

    public static bool TryGet(int cfgId, out SkillInfo info)
    {
        var found = Getter?.Invoke(cfgId);
        info = found ?? default;
        return found is not null;
    }
}
