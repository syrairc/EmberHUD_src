using System;

namespace Mcp;

internal static class Registry
{
    public static Tool[] Build(Func<Status> status, Func<int> maxRead, Watches watches, Recorder recorder, Recordings recordings) =>
    [
        .. StateTools.Create(status),
        .. MemoryTools.Create(maxRead),
        .. UeTools.Create(),
        .. WatchTools.Create(watches),
        .. RecordTools.Create(recorder, recordings),
    ];
}
