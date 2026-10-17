namespace EmberHUD;

public enum TartarusBeast : byte { Unknown, Mammoth, Lion, Wolf, Rat }

public readonly record struct TartarusCell(int Index, bool Open, int Tier, TartarusBeast Beast, bool DevourTarget, bool EventMarker, int Uid = 0);

public enum TartarusChoiceKind : byte { Doors, Operation, Office, Instrument, Reward, Event, Other }

public enum TartarusOptionKind : byte { Door, Consume, Replace, End, StopChain, Instigate, Bestow, Instrument, Reward, Confirm, Other }

public readonly record struct TartarusOption(int Id, TartarusOptionKind Kind, int Cell, int Param);

public sealed record TartarusChoice(int Id, TartarusChoiceKind Kind, TartarusOption[] Options, bool Complete);

public sealed record TartarusState(int Position, int Size, int Day, int MaxDay, TartarusCell[] Cells, TartarusChoice? Choice, int[] Instruments, int Chromatics);
