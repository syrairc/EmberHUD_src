namespace Aember;

public sealed class BackgroundJob<T>(bool inline = false)
{
    private Task<T>? _task;

    public bool Running => _task is not null;

    public void Start(Func<T> work)
    {
        if (_task is not null) throw new InvalidOperationException("the previous job has not been taken yet");
        _task = inline ? RunInline(work) : Task.Run(work);
    }

    public Task<T>? TakeFinished()
    {
        if (_task is not { IsCompleted: true } done) return null;
        _task = null;
        return done;
    }

    private static Task<T> RunInline(Func<T> work)
    {
        try
        {
            return Task.FromResult(work());
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }
}
