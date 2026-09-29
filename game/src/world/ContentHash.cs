using System;
using System.IO;

/// World-query "Content hash" (W-12): a 64-bit hash of the files a world is built from. The physics log records it, so a
/// replay can refuse to run on a mismatched world. The rule (game/maps/README.md, Content hash):
/// - The files, in this order: the package's map.json, height.r16, surface.png, cover.png and objects.json, then the
///   shared surfaces.json and catalog.json. Only these names are read, so the folder, other files and the order in which
///   the file system lists them never count.
/// - Each file adds its bytes, then their count as 8 bytes little-endian. In the JSON files every CR LF pair counts as a
///   single LF (and the count is after that), so CRLF and LF checkouts hash the same. The other files count byte for byte.
/// - The hash is FNV-1a 64 over that stream: offset basis 0xCBF29CE484222325, prime 0x100000001B3.
/// Every FNV-1a step is a bijection of the state, so two streams of the same length that differ in one byte always hash
/// differently: a change to one byte of a binary layer, or to a JSON byte that neither is nor becomes a CR or LF, always
/// changes the hash. Any other change changes it with probability 1 − 2⁻⁶⁴.
public sealed partial class WorldQuery
{
    internal const ulong FnvOffset = 0xCBF29CE484222325UL;
    const ulong FnvPrime = 0x100000001B3UL;

    /// The version of the query math. The content hash covers only the data, so this is raised with every code change
    /// that alters any query result for the same data, and the golden file is re-recorded with it. The physics log
    /// records it next to ContentHash, and a replay refuses a log whose version differs. 1: the math of task 3.5.
    /// 2: lying elements lie straight from the mat top at the root to the mat top at the tip (API review F1).
    public const int QueryVersion = 2;

    /// The content hash of the world this instance was loaded from (ContentHashOf); 0 for a world built in memory.
    public ulong ContentHash { get; private set; }

    /// The content hash of a map package with the shared tables, from the files alone: a replay can check it before
    /// loading anything.
    public static ulong ContentHashOf(string packageDir, string surfacesPath, string catalogPath)
    {
        ulong hash = FnvOffset;
        foreach (string path in new[]
        {
            Path.Combine(packageDir, "map.json"), Path.Combine(packageDir, "height.r16"), Path.Combine(packageDir, "surface.png"),
            Path.Combine(packageDir, "cover.png"), Path.Combine(packageDir, "objects.json"), surfacesPath, catalogPath,
        })
            hash = HashFile(hash, File.ReadAllBytes(path), path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        return hash;
    }

    /// Continues `hash` over one file: its bytes (with CR LF as LF when `text`), then their count.
    internal static ulong HashFile(ulong hash, ReadOnlySpan<byte> bytes, bool text)
    {
        long count = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (text && bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n')
                continue;
            hash = (hash ^ bytes[i]) * FnvPrime;
            count++;
        }
        for (int k = 0; k < 8; k++)
            hash = (hash ^ (byte)(count >> 8 * k)) * FnvPrime;
        return hash;
    }
}
