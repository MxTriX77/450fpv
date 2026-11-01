using System.Text;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    public sealed class WorldHashTests
    {
        private static ulong Hash(string text, bool asText) =>
            WorldHash.HashFile(WorldHash.Offset, Encoding.UTF8.GetBytes(text), asText);

        // Expected values from an independent FNV-1a 64 (prime 0x100000001B3), whose
        // one-byte case gives the published vector FNV-1a("a") = 0xAF63DC4C8601EC8C (derived).
        [Test]
        public void HashFile_IsFnv1aOverTheBytesThenTheirCount()
        {
            Assert.That(Hash("", false), Is.EqualTo(0xA8C7F832281A39C5UL));
            Assert.That(Hash("abc", false), Is.EqualTo(0xE5F49FAFDCDF1048UL));
        }

        [Test]
        public void HashFile_ReadsCrLfAsLfInTextOnly()
        {
            Assert.That(Hash("a\r\nb", true), Is.EqualTo(Hash("a\nb", true)));
            Assert.That(Hash("a\r\nb", true), Is.EqualTo(0xBE8039E57ECCF2D3UL));
            Assert.That(Hash("a\r\nb", false), Is.EqualTo(0x6B47F7673241B33FUL));
            Assert.That(Hash("a\rb", true), Is.Not.EqualTo(Hash("ab", true)), "a lone CR is a byte like any other");
        }

        [Test]
        public void HashFile_OneByteChangesIt()
        {
            Assert.That(Hash("stations 1", true), Is.Not.EqualTo(Hash("stations 2", true)));
        }

        [Test]
        public void QueryVersion_IsTwo()
        {
            Assert.That(WorldHash.QueryVersion, Is.EqualTo(2));
        }
    }
}
