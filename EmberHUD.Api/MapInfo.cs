namespace EmberHUD;

public enum ContentState : byte { Pending, Active, Done }

public readonly record struct MapContent(string Name, string Icon, ContentState State, int Done = 0, int Total = 1);

public sealed record MapInfo(string Name, int MonsterLevel, int? Remaining, string? XpGain, bool IsTown, MapContent[] Content, string[] Modifiers);
