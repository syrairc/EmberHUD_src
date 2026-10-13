namespace EmberHUD.Host.Memory;

internal interface IReadBackend
{
    bool TryRead(nint address, Span<byte> target);
}
