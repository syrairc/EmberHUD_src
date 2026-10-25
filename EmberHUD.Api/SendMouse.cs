using System.Runtime.InteropServices;

namespace EmberHUD;

public enum MouseMode : byte { Direct, Humanized }

public static partial class Send
{
    const int MaxPendingJobs = 32;

    static readonly Queue<MouseJob> Pending = new();
    static readonly SemaphoreSlim PendingSignal = new(0);
    static long _generation;
    static Thread? _worker;
    static nint _waitTimer;

    internal static MouseMode Mode { get; set; } = MouseMode.Humanized;

    internal static HumanizerConfig Config { get; set; } = new();

    internal static Func<(int X, int Y)> CursorPosition { get; set; } = ReadCursor;

    internal static Action<int> Wait { get; set; } = HighResolutionWait;

    internal static Random Rng { get; set; } = Random.Shared;

    /// <summary>
    /// Queues a cursor move and returns when the cursor is within the humanizer's StopTolerance of the target (humanized) or the single move was sent (direct). Calls run in order on one worker. True only if every event was sent.
    /// The immediate <see cref="MouseMove"/>, <see cref="Click"/>, <see cref="MouseDown"/> and <see cref="MouseUp"/> bypass the queue and can interleave with an in-flight call.
    /// </summary>
    public static Task<bool> MoveTo(int x, int y, MouseMode? mode = null, CancellationToken cancel = default, HumanizerConfig? config = null) =>
        Enqueue(new MouseJob(x, y, null, mode, cancel, config));

    /// <summary>Queues a move, then a click with the given button. Same ordering and result rules as <see cref="MoveTo"/>; a click refused by the rate limit leaves the cursor on the target and returns false.</summary>
    public static Task<bool> ClickAt(int x, int y, MouseButton button = MouseButton.Left, MouseMode? mode = null, CancellationToken cancel = default, HumanizerConfig? config = null) =>
        Enqueue(new MouseJob(x, y, button, mode, cancel, config));

    /// <summary>The screen pixel for a game client pixel, the same conversion <see cref="MoveTo"/> uses. Null without a game window.</summary>
    public static (int X, int Y)? ClientToScreen(int x, int y) => ToScreen?.Invoke(x, y);

    /// <summary>Inverse of <see cref="ClientToScreen"/>, a plain translation from the client origin.</summary>
    public static (int X, int Y)? ScreenToClient(int x, int y) =>
        ClientToScreen(0, 0) is var (originX, originY) ? (x - originX, y - originY) : null;

    /// <summary>The cursor in screen pixels.</summary>
    public static (int X, int Y) CursorScreen() => CursorPosition();

    static void AbortQueuedMouse()
    {
        _generation++;
        while (Pending.TryDequeue(out var job)) job.Done.TrySetResult(false);
    }

    static void ResetMouseSeams()
    {
        Mode = MouseMode.Humanized;
        Config = new HumanizerConfig();
        CursorPosition = ReadCursor;
        Wait = HighResolutionWait;
        Rng = Random.Shared;
    }

    static Task<bool> Enqueue(MouseJob job)
    {
        lock (Gate)
        {
            if (Pending.Count >= MaxPendingJobs) return Task.FromResult(false);
            job.Generation = _generation;
            Pending.Enqueue(job);
            _worker ??= StartWorker();
        }
        PendingSignal.Release();
        return job.Done.Task;
    }

    static Thread StartWorker()
    {
        var worker = new Thread(WorkerLoop) { IsBackground = true, Name = "EmberHUD input" };
        worker.Start();
        return worker;
    }

    static void WorkerLoop()
    {
        while (true)
        {
            PendingSignal.Wait();
            MouseJob? job;
            lock (Gate) Pending.TryDequeue(out job);
            if (job is null) continue;
            var ok = false;
            try
            {
                ok = Run(job);
            }
            catch (Exception exception)
            {
                Log.Warn($"Send worker: {exception.Message}");
            }
            job.Done.TrySetResult(ok);
        }
    }

    static bool Run(MouseJob job)
    {
        if (ToScreen?.Invoke(job.X, job.Y) is not var (screenX, screenY)) return false;
        var humanized = (job.Mode ?? Mode) == MouseMode.Humanized;
        var config = (job.Config ?? Config).Clamped();
        if (!MoveCursor(job, screenX, screenY, humanized, config)) return false;
        if (job.Button is not { } button) return true;

        if (humanized) Wait(Rng.Next(config.PreClickSettleMin, config.PreClickSettleMax + 1));
        try
        {
            if (!Emit(job, new SentInput(Key.None, button, true, 0, 0), capped: true)) return false;
            if (humanized) Wait(Rng.Next(config.ClickDwellMin, config.ClickDwellMax + 1));
        }
        finally
        {
            if (job.Pressed) ReleaseHeld(button);
        }
        if (humanized) Wait(Rng.Next(config.PostClickSettleMin, config.PostClickSettleMax + 1));
        return true;
    }

    static bool MoveCursor(MouseJob job, int screenX, int screenY, bool humanized, HumanizerConfig config)
    {
        if (!humanized) return Emit(job, new SentInput(Key.None, null, false, screenX, screenY), capped: false);
        foreach (var step in MousePath.Generate(CursorPosition(), (screenX, screenY), config, Rng))
        {
            if (!Emit(job, new SentInput(Key.None, null, false, step.X, step.Y), capped: false)) return false;
            Wait(step.DelayMs);
        }
        return true;
    }

    static bool Alive(MouseJob job)
    {
        lock (Gate) return Allowed && !job.Cancel.IsCancellationRequested && job.Generation == _generation;
    }

    static bool Emit(MouseJob job, SentInput input, bool capped)
    {
        lock (Gate)
        {
            if (!Allowed || job.Cancel.IsCancellationRequested || job.Generation != _generation) return false;
            if (capped && !Admit()) return false;
            if (input.Button is { } button && input.Down)
            {
                HeldButtons.Add(button);
                job.Pressed = true;
            }
            Sink(input);
            return true;
        }
    }

    static void ReleaseHeld(MouseButton button)
    {
        lock (Gate)
        {
            if (HeldButtons.Remove(button)) Sink(new SentInput(Key.None, button, false, 0, 0));
        }
    }

    static (int X, int Y) ReadCursor() => GetCursorPos(out var point) != 0 ? (point.X, point.Y) : (0, 0);

    static void HighResolutionWait(int milliseconds)
    {
        if (milliseconds <= 0) return;
        if (_waitTimer == 0) _waitTimer = CreateWaitableTimerExW(0, 0, HighResolutionTimerFlag, TimerAllAccess);
        var due = -milliseconds * 10_000L;
        if (_waitTimer != 0 && SetWaitableTimer(_waitTimer, in due, 0, 0, 0, 0) != 0) WaitForSingleObject(_waitTimer, uint.MaxValue);
        else Thread.Sleep(milliseconds);
    }

    const uint HighResolutionTimerFlag = 0x2;
    const uint TimerAllAccess = 0x1F0003;

    sealed class MouseJob(int x, int y, MouseButton? button, MouseMode? mode, CancellationToken cancel, HumanizerConfig? config)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
        public MouseButton? Button { get; } = button;
        public MouseMode? Mode { get; } = mode;
        public CancellationToken Cancel { get; } = cancel;
        public HumanizerConfig? Config { get; } = config;
        public long Generation { get; set; }
        public bool Pressed { get; set; }
        public TaskCompletionSource<bool> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    private static partial int GetCursorPos(out NativePoint point);

    [LibraryImport("kernel32.dll")]
    private static partial nint CreateWaitableTimerExW(nint attributes, nint name, uint flags, uint access);

    [LibraryImport("kernel32.dll")]
    private static partial int SetWaitableTimer(nint timer, in long dueTime, int period, nint routine, nint argument, int resume);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(nint handle, uint milliseconds);
}
