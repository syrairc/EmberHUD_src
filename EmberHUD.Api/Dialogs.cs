namespace EmberHUD;

public enum DialogKind { OpenFile, OpenFiles, SaveFile, PickFolder }

public sealed record DialogFilter(string Name, string Pattern);

public sealed record DialogSpec(DialogKind Kind, string Title, string? StartFolder, string? FileName, IReadOnlyList<DialogFilter> Filters);

public sealed class DialogRequest
{
    private string[]? _paths;
    private bool _taken;

    public bool Done => Volatile.Read(ref _paths) is not null;

    public string? Path => Volatile.Read(ref _paths) is { Length: > 0 } paths ? paths[0] : null;

    public bool TryTake(out string[] paths)
    {
        var finished = Volatile.Read(ref _paths);
        if (finished is null || _taken)
        {
            paths = [];
            return false;
        }
        _taken = true;
        paths = finished;
        return true;
    }

    internal void Complete(string[] paths) => Volatile.Write(ref _paths, paths);
}

// Native Windows file / folder dialogs on their own STA thread. Start returns at once, poll Done / TryTake from Draw. One dialog at a time, a second request while one is open comes back cancelled
public static class Dialogs
{
    private static int _busy;

    internal static Func<DialogSpec, string[]> Runner { get; set; } = NativeDialog.Run;

    public static bool Busy => Volatile.Read(ref _busy) == 1;

    public static DialogRequest OpenFile(string title, string? startFolder = null, params DialogFilter[] filters) =>
        Start(new DialogSpec(DialogKind.OpenFile, title, startFolder, null, filters));

    public static DialogRequest OpenFiles(string title, string? startFolder = null, params DialogFilter[] filters) =>
        Start(new DialogSpec(DialogKind.OpenFiles, title, startFolder, null, filters));

    public static DialogRequest SaveFile(string title, string? startFolder = null, string? fileName = null, params DialogFilter[] filters) =>
        Start(new DialogSpec(DialogKind.SaveFile, title, startFolder, fileName, filters));

    public static DialogRequest PickFolder(string title, string? startFolder = null) =>
        Start(new DialogSpec(DialogKind.PickFolder, title, startFolder, null, []));

    internal static void Reset()
    {
        Runner = NativeDialog.Run;
        Volatile.Write(ref _busy, 0);
    }

    private static DialogRequest Start(DialogSpec spec)
    {
        var request = new DialogRequest();
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            request.Complete([]);
            return request;
        }

        var runner = Runner;
        var thread = new Thread(() =>
        {
            string[] paths;
            try
            {
                paths = runner(spec);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "file dialog");
                paths = [];
            }
            Volatile.Write(ref _busy, 0);
            request.Complete(paths);
        })
        { IsBackground = true, Name = "Ember file dialog" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return request;
    }
}
