using System;
using System.Collections;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// Logs that the Boot scene loaded. With <c>-smoketest</c> on the command line the player
    /// quits on the next frame, so a batchmode run proves that a build opens its scene.
    /// </summary>
    public sealed class Boot : MonoBehaviour
    {
        IEnumerator Start()
        {
            Debug.Log("[Boot] Boot scene loaded");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-smoketest") < 0)
                yield break;

            yield return null;
            Application.Quit(0);
        }
    }
}
