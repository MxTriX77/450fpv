using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The repository's drone.json (assets-src/drone/drone.json), found from this file's own path (the .NET tools
    // compile it in place) or from Unity's project folder (unity/, the editor's working directory).
    public static class BootstrapDrone
    {
        public const string RelativePath = "assets-src/drone/drone.json";

        static DroneSpec _spec;

        public static string Text() => File.ReadAllText(FilePath());

        public static DroneSpec Spec() => _spec ?? (_spec = DroneSpec.Parse(Text()));

        public static string FilePath([CallerFilePath] string here = "")
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(here))
                candidates.Add(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(here)) ?? "", "..", "..", "..", "..", RelativePath));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", RelativePath));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), RelativePath));
            foreach (string c in candidates)
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            Assert.Fail(RelativePath + " was not found from " + here);
            return null;
        }
    }
}
