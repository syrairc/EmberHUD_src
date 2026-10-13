using System.Numerics;

namespace Aember;

public sealed record TradePage(int Page, bool LastPage, int CellsPerPage, TradeListing[] Listings, string PriceIcon);

public readonly record struct TradeListing(int Slot, string Name, long Quantity, long Price, string? Status, Vector2 Min, Vector2 Max);
