using Aember;
using Aember.Testing;

namespace Inspector;

public sealed class SnapshotsTests
{
    long _now;

    int ReadInt(Snapshots snapshots, nint at) => snapshots.Get(at, 4) is { } bytes ? System.BitConverter.ToInt32(bytes) : -1;

    [Test]
    public void BytesRefreshAfterTheIntervalAndHoldWhileFrozen()
    {
        var at = TestMemory.Alloc(8);
        TestMemory.Write(at, 1);
        var snapshots = new Snapshots(() => _now) { RefreshRate = 10 };

        Check.Equal(1, ReadInt(snapshots, at));
        TestMemory.Write(at, 2);
        _now = 50;
        Check.Equal(1, ReadInt(snapshots, at));
        _now = 100;
        Check.Equal(2, ReadInt(snapshots, at));

        snapshots.Frozen = true;
        TestMemory.Write(at, 3);
        _now = 500;
        Check.Equal(2, ReadInt(snapshots, at));
    }

    [Test]
    public void ANewBlockReadsOnceEvenWhenFrozen()
    {
        var at = TestMemory.Alloc(8);
        TestMemory.Write(at, 4);
        var snapshots = new Snapshots(() => _now) { Frozen = true };

        Check.Equal(4, ReadInt(snapshots, at));
    }

    [Test]
    public void UnreadableIsNullAndUnusedEntriesAreDropped()
    {
        var at = TestMemory.Alloc(8);
        var snapshots = new Snapshots(() => _now);

        Check.That(snapshots.Get(at + 4, 8) is null, "read past the end");
        snapshots.Get(at, 4);
        TestMemory.Write(at, 9);
        _now = 2000;
        snapshots.Prune();
        _now = 2001;
        Check.Equal(9, ReadInt(snapshots, at));
    }

    [Test]
    public void GarbageLengthsAreNull()
    {
        var at = TestMemory.Alloc(8);
        var snapshots = new Snapshots(() => _now);

        Check.That(snapshots.Get(at, -1) is null, "negative length");
        Check.That(snapshots.Get(at, 0) is null, "zero length");
        Check.That(snapshots.Get(at, int.MaxValue) is null, "huge length");
    }
}
