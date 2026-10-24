using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class DeathReader(IReadBackend memory, nint moduleBase, GameUiReader ui)
{
    private readonly LuaReader _lua = new(memory, moduleBase);

    public DeathState? Read(GameUi state)
    {
        var panel = Array.Find(state.Panels, p => p.Name == Offsets.DeathPanelClassName);
        if (panel.Address == 0 || !_lua.TryOpen(out var globals) || !_lua.TryResolve(globals, Offsets.LuaDieNotify, out var notify, out _) ||
            !_lua.TryGetNumber(notify, Offsets.LuaDieReduceRate, out var rate) || rate is < 0 or > 100)
            return null;

        var canUse = _lua.TryGetBool(notify, Offsets.LuaDieCanUseCoin, out var can) && can;
        return new DeathState((float)rate, canUse, canUse ? ui.ReadDeathToken(panel.Address) : false);
    }
}
