using System.IO;
using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The D3D12 pipeline library kept empty (w1-farmland-world 2.2, design 6): the player's file becomes an empty
    /// read-only file, whatever was there, and stays so.
    /// </summary>
    public sealed class PsoLibraryTests
    {
        string dir, path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "fpv450-psolibrary-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, PsoLibrary.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(path))
                File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }

        static void AssertKeptEmpty(string path)
        {
            var file = new FileInfo(path);
            Assert.That(file.Exists, Is.True);
            Assert.That(file.Length, Is.EqualTo(0));
            Assert.That(file.IsReadOnly, Is.True);
        }

        [Test]
        public void ALibraryFromEarlierRunsIsDropped()
        {
            File.WriteAllBytes(path, new byte[] { 0xBC, 0x1B, 0x59, 0x28 });
            Assert.That(PsoLibrary.KeepEmpty(path), Is.True);
            AssertKeptEmpty(path);
        }

        [Test]
        public void NoLibraryLeavesAnEmptyOne()
        {
            Assert.That(PsoLibrary.KeepEmpty(path), Is.True);
            AssertKeptEmpty(path);
        }

        [Test]
        public void AnEmptyLibraryIsLeftAlone()
        {
            PsoLibrary.KeepEmpty(path);
            Assert.That(PsoLibrary.KeepEmpty(path), Is.False);
            AssertKeptEmpty(path);
        }

        [Test]
        public void AWritableEmptyFileIsMadeReadOnly()
        {
            File.WriteAllBytes(path, new byte[0]);
            Assert.That(PsoLibrary.KeepEmpty(path), Is.True);
            AssertKeptEmpty(path);
        }
    }
}
