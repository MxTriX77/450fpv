using NUnit.Framework;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The farmland (w1-farmland-world, farmland-world spec, "A fresh worktree gets the world"): the friend's scene opens
    /// with every reference found, its pushed text and the local store's linked parts together. Walks the scene's
    /// objects, the terrains' tree and detail prototypes and layers, and the sky (SceneReferences, which the player's
    /// build checks too). Needs <c>pwsh tools/vendor/link.ps1</c>.
    /// </summary>
    public sealed class FarmLandSceneTests
    {
        const string ScenePath = "Assets/Farm_land/Scenes/FarmLand.unity";

        [Test]
        public void Scene_OpensWithEveryReferenceFound()
        {
            Assert.That(SceneReferences.Missing(ScenePath), Is.Empty, "missing in the farmland; in a fresh checkout run pwsh tools/vendor/link.ps1 first");
        }
    }
}
