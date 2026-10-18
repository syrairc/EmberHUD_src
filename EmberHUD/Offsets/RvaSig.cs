namespace EmberHUD.Host;

internal sealed record RvaSig(string Key, string Pattern, int DispOffset, int EndOffset);

internal sealed record RvaRelation(string Key, string Other, int Delta);
