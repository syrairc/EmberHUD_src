using System.Linq;

namespace Tartarus.Sim;

public sealed partial class Run
{
    void Bestow()
    {
        Lift();
        Phase = Phase.Manage;
    }

    void Instigate()
    {
        Plant();
        Phase = Phase.Manage;
    }

    void Lift()
    {
        var inmates = Inmates();
        if (inmates.Count > 0) Raise(inmates[Pick(inmates.Count)], 1);
    }

    void Plant()
    {
        var free = LockedCells().Where(cell => Cells[cell].Event is null && !Cells[cell].Markers.HasFlag(Marker.Nest)).ToList();
        foreach (var cell in Sample(free, 2)) Cells[cell] = Cells[cell] with { Event = RandomEvent(), Seen = true };
    }

    void Tyranny()
    {
        for (var i = 0; i < 3; i++) Plant();
        for (var i = 0; i < 3; i++) Lift();
    }
}
