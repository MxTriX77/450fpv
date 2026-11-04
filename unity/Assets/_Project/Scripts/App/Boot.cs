using Fpv450.Input;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The Boot scene's: logs that it loaded and opens the app (AppFlow, the menu). The scene's camera keeps its noclip
    /// switched off, so the menu has the mouse. With <c>-smoketest</c> the app walks its screens and flights and quits
    /// (AppFlow.Smoke), so a batchmode run proves that a build opens and flies.
    /// </summary>
    public sealed class Boot : MonoBehaviour
    {
        void Awake()
        {
            Debug.Log("[Boot] Boot scene loaded");
            var noclip = GetComponent<NoclipCamera>();
            if (noclip != null) noclip.enabled = false;
            AppFlow.Create();
        }
    }
}
