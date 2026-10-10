namespace TorchlightSDK.Host;

internal static partial class Offsets
{
    internal static ReadOnlySpan<byte> Seed => [0x1A, 0x71, 0x10, 0x0F, 0x18, 0x27, 0x6A, 0x82, 0x63, 0xDE, 0x9E, 0x76, 0xEF, 0x00, 0x52, 0x9D, 0xB5, 0xE4, 0xD2, 0xEA, 0xF6, 0xBA, 0x5D, 0xC9, 0xD8, 0x17, 0xDE, 0x37, 0x29, 0xBB, 0x8A, 0xEC];
    internal static int KnownExeSize => T[0];
    internal static int LogicWorldGlobalRva => T[1];
    internal static int WorldScene => T[2];
    internal static int SceneGeometry => T[3];
    internal static int SceneGridModule => T[4];
    internal static int SceneEntityBuckets => T[5];
    internal static int SceneEntityBucketCount => T[6];
    internal static int NodeKey => T[7];
    internal static int NodeEntity => T[8];
    internal static int NodeNext => T[9];
    internal static int GeometryCellSize => T[10];
    internal static int GeometryOddRowShift => T[11];
    internal static int GeometryOriginX => T[12];
    internal static int GeometryOriginY => T[13];
    internal static int GridModuleHybrid => T[14];
    internal static int HybridCols => T[15];
    internal static int HybridRows => T[16];
    internal static int HybridCells => T[17];
    internal static int HybridIsHybrid => T[18];
    internal static int HybridBlockW => T[19];
    internal static int HybridBlockH => T[20];
    internal static int HybridBlocksPerRow => T[21];
    internal static int HybridMask => T[22];
    internal static int HybridBlockCount => T[23];
    internal static int CellStride => T[24];
    internal static int CellClearance => T[25];
    internal static int EntityCfgId => T[26];
    internal static int EntityAlive => T[27];
    internal static int EntityHasRole => T[28];
    internal static int EntityHasPosition => T[29];
    internal static int EntityPosition => T[30];
    internal static int PositionX => T[31];
    internal static int MaxEntities => T[32];
    internal static int MaxBuckets => T[33];
    internal static int MaxChain => T[34];
    internal static int MaxCells => T[35];
    internal static int PebImageBase => T[36];
}
