using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The repository's drone.json (assets-src/drone/drone.json): p5-drone's researched Svinoriz 10 Opto on letter T's
    // layout (D-043).
    public static class RepositoryDrone
    {
        public const string RelativePath = "assets-src/drone/drone.json";

        static DroneSpec _spec;

        public static DroneSpec Spec() => _spec ?? (_spec = DroneSpec.Parse(File.ReadAllText(TestFiles.Find(RelativePath))));
    }

    // p6-flight-core's bootstrap drone.json on K§5.2's assumed layout, kept as a fixture when p5-drone replaced the
    // repository's file, so the K§5.2 cross-check and the rigid-body tests that need its large product of inertia
    // still run on it.
    public static class BootstrapDrone
    {
        public const string RelativePath = "unity/Packages/com.fpv450.sim/Tests/Fixtures/drone-bootstrap.json";

        static DroneSpec _spec;

        public static DroneSpec Spec() => _spec ?? (_spec = DroneSpec.Parse(File.ReadAllText(TestFiles.Find(RelativePath))));
    }

    // A repository file, found from this file's own path (the .NET tools compile it in place) or from Unity's project
    // folder (unity/, the editor's working directory).
    public static class TestFiles
    {
        public static string Find(string relativeToRepository, [CallerFilePath] string here = "")
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(here))
                candidates.Add(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(here)) ?? "", "..", "..", "..", "..", relativeToRepository));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", relativeToRepository));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), relativeToRepository));
            foreach (string c in candidates)
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            Assert.Fail(relativeToRepository + " was not found from " + here);
            return null;
        }
    }
}
