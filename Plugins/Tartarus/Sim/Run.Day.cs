namespace Tartarus.Sim;

public sealed partial class Run
{
    void EndDay()
    {
        Chaining = -1;
        if (Day >= MaxDay)
        {
            Phase = Phase.Ended;
            Queue.Clear();
            return;
        }
        Day++;
        Consumed = Replaced = false;
        Morning();
        Phase = Phase.Patrol;
        RollDoors();
    }
}
