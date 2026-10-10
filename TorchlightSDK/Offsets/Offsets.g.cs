namespace TorchlightSDK.Host;

internal static partial class Offsets
{
    internal static ReadOnlySpan<byte> Seed => [0xEF, 0x0B, 0xA8, 0xB3, 0x15, 0xC2, 0xA6, 0xC7, 0x4E, 0x94, 0xB5, 0x94, 0x8A, 0x7D, 0x51, 0x0A, 0x2D, 0xF0, 0x80, 0x36, 0xE4, 0x98, 0x8A, 0xEE, 0x2D, 0xA8, 0x04, 0x2F, 0x16, 0x2E, 0xBE, 0xD0];
    internal static int KnownExeSize => T[0];
    internal static int LogicWorldGlobalRva => T[1];
    internal static int WorldScene => T[2];
    internal static int SceneGeometry => T[3];
    internal static int SceneGridModule => T[4];
    internal static int SceneEntityArray => T[5];
    internal static int SceneEntityCount => T[6];
    internal static int GeometryCellSize => T[7];
    internal static int GeometryOddRowShift => T[8];
    internal static int GeometryOriginX => T[9];
    internal static int GeometryOriginY => T[10];
    internal static int GridModuleHybrid => T[11];
    internal static int HybridCols => T[12];
    internal static int HybridRows => T[13];
    internal static int HybridCells => T[14];
    internal static int HybridIsHybrid => T[15];
    internal static int HybridBlockW => T[16];
    internal static int HybridBlockH => T[17];
    internal static int HybridBlocksPerRow => T[18];
    internal static int HybridMask => T[19];
    internal static int HybridBlockCount => T[20];
    internal static int CellStride => T[21];
    internal static int CellClearance => T[22];
    internal static int EntityCfgId => T[23];
    internal static int EntityAlive => T[24];
    internal static int EntityHasPosition => T[25];
    internal static int EntityPosition => T[26];
    internal static int PositionX => T[27];
    internal static int MaxEntities => T[28];
    internal static int MaxCells => T[29];
    internal static int PebImageBase => T[30];
}
