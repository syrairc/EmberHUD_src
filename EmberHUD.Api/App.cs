using System;

namespace EmberHUD;

public static class App
{
    internal static Action? Quitter { get; set; }

    public static void Quit() => Quitter?.Invoke();
}
