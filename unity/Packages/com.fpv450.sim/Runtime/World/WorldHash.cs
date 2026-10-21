// WorldHash: the identity of a world (K§10.4, design 6). A 64-bit FNV-1a hash of the
// data it is built from, plus QueryVersion for the math. The flight log records both,
// so a replay can refuse a mismatched world.
//
// Ported from the first attempt (D-027, read from refs/godot/main and never merged):
// refs/godot/main:game/src/world/ContentHash.cs (FnvOffset, FnvPrime, HashFile,
// QueryVersion). The file list and the shared-table parts are the gallery loader's
// (task 1.3: stations.json and every referenced .assetmeta.json), not ported here.
//
// The rule: each file continues the hash with its bytes, then their count as 8 bytes
// little-endian. In a text file every CR LF pair counts as a single LF (and the count
// is taken after that), so CRLF and LF checkouts hash the same. Every FNV-1a step is a
// bijection of the state, so two streams of the same length that differ in one byte
// always hash differently.

using System;

namespace Fpv450.Sim.World
{
    public static class WorldHash
    {
        /// <summary>
        /// The version of the query math. The content hash covers only the data, so this is
        /// raised with every code change that alters any query result for the same data,
        /// and the golden file is re-recorded with it (audited case by case).
        /// 1: the interface of p6-world-query-gallery task 1.1 and the floor stub.
        /// </summary>
        public const int QueryVersion = 1;

        /// <summary>FNV-1a 64's offset basis: the hash of nothing.</summary>
        public const ulong Offset = 0xCBF29CE484222325UL;

        private const ulong Prime = 0x100000001B3UL;

        /// <summary>Continues `hash` over one file: its bytes (CR LF read as LF when `text`), then their count.</summary>
        public static ulong HashFile(ulong hash, ReadOnlySpan<byte> bytes, bool text)
        {
            long count = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (text && bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n')
                    continue;
                hash = (hash ^ bytes[i]) * Prime;
                count++;
            }
            for (int k = 0; k < 8; k++)
                hash = (hash ^ (byte)(count >> 8 * k)) * Prime;
            return hash;
        }
    }
}
