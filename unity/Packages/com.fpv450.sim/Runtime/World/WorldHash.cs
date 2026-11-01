// WorldHash: the identity of a world (K§10.4, design 6). A 64-bit FNV-1a hash of the
// data it is built from, plus QueryVersion for the math. The flight log records both,
// so a replay can refuse a mismatched world.
//
// Ported from the first attempt (D-027, read from refs/godot/main and never merged):
// refs/godot/main:game/src/world/ContentHash.cs (FnvOffset, FnvPrime, HashFile,
// QueryVersion). The file lists are each world's own, not ported:
// - MapWorld (task 1.4): the placement file, then every .assetmeta.json it places, once
//   each, in ordinal order of their paths below MapWorld.ModelsRoot, then its material
//   names as one text ("materials", then each name, LF-separated). A row, a pose or an
//   asset's metadata that changes by one byte changes the hash.
// - FloorWorld: its constructor's arguments.
// Runtime objects (the launch stand) are outside the hash; the flight log records them.
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
        /// 2 (task 1.5): a capsule's closest approach to a box it stays outside is exact
        /// (ShapeMath.BoxMinimizer, within 3.3e-12 m of version 1's search on the audit set),
        /// and a box sweep that provably cannot touch ends instead of giving up as a touch;
        /// MapWorld's wires, patches and hole bands are new. golden/floor.txt re-recorded.
        /// 3 (QA's review B3): a first touch is never a give-up. The plane's first touch is exact
        /// (its distance is linear in t), a separating plane proves when a sweep cannot touch any
        /// shape (ShapeMath.Bound), and after 64 distance / speed steps the sweep steps by that
        /// bound; a sweep still unsettled after 128 counts no first touch. Both golden files
        /// re-recorded: only first touches on the plane and former give-ups moved.
        /// </summary>
        public const int QueryVersion = 3;

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
