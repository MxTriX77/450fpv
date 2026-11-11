using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Feed
{
    /// <summary>
    /// Starts every D3D12 player run with an empty pipeline library (w1-farmland-world 2.2, design 6). Unity keeps one
    /// <c>ID3D12PipelineLibrary</c> for every build of the product in <c>dx12_pso_cache_lib.bin</c> under
    /// <see cref="Application.temporaryCachePath"/>, loads it before any script runs and serializes it back at quit. An
    /// entry left by an earlier run can carry the same name as one of this run's PSOs with another description; the
    /// load then fails, Unity stores the new PSO under the taken name, and the D3D12 runtime rejects the store but keeps
    /// the PSO's shader pointers in the library. Once that PSO is gone, the library's serialization at quit reads freed
    /// memory: the access violation in D3D12Core.dll that the farmland with the feed hit on quit.
    /// </summary>
    /// <remarks>
    /// The file is kept empty and read-only, so each run's library starts empty and is never written back: the player's
    /// log shows Unity's "d3d12: PSO Cache Library Init Failure" at the start and "Error when writing PSO binary data" at
    /// quit, both expected. A run that finds an older library has already loaded it; from the next run on, none is.
    /// </remarks>
    public static class PsoLibrary
    {
        public const string FileName = "dx12_pso_cache_lib.bin";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Startup()
        {
            if (Application.isEditor || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                return;
            string path = Path.Combine(Application.temporaryCachePath, FileName);
            try
            {
                if (KeepEmpty(path))
                    Debug.Log("[Feed] the D3D12 pipeline library from earlier runs is dropped; from the next run on, each starts empty (" + path + ")");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Feed] the D3D12 pipeline library could not be kept empty: " + e.Message);
            }
        }

        /// <summary>Makes <paramref name="path"/> an empty read-only file; true when it was anything else.</summary>
        public static bool KeepEmpty(string path)
        {
            var file = new FileInfo(path);
            if (file.Exists && file.Length == 0 && file.IsReadOnly)
                return false;
            if (file.Exists)
            {
                file.IsReadOnly = false;
                file.Delete();
            }
            File.WriteAllBytes(path, Array.Empty<byte>());
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            return true;
        }
    }
}
