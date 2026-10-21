namespace EmberHUD.Assets;

internal sealed class JobRunner(Action<string>? log = null)
{
    readonly object gate = new();
    CancellationTokenSource? cancel;
    volatile bool busy;
    volatile string progress = "";
    volatile AssetResult? last;
    volatile string? failure;

    public bool Busy => busy;
    public string Progress => progress;
    public AssetResult? Last => last;
    public string? Failure => failure;

    public bool TryStart(IAssetJob job, IReadOnlyList<string> paks, string iconsRoot, byte[] key)
    {
        CancellationTokenSource source;
        lock (gate)
        {
            if (busy) return false;
            busy = true;
            failure = null;
            last = null;
            progress = "starting";
            cancel = source = new CancellationTokenSource();
        }
        Task.Run(() =>
        {
            try
            {
                last = job.Run(paks, iconsRoot, key, text => progress = text, source.Token);
            }
            catch (OperationCanceledException)
            {
                failure = "cancelled";
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                log?.Invoke(ex.ToString());
            }
            finally
            {
                source.Dispose();
                busy = false;
            }
        });
        return true;
    }

    public void Cancel()
    {
        lock (gate)
        {
            try
            {
                if (busy) cancel?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
