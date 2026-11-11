using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Step 13: camera feel. The free-fly camera tilts into strafes and turns by itself (FreeFlyCamera), and
/// the post-processing profile adds motion blur from the camera's own movement. The blur goes into the Step 9
/// profile, so this step re-runs Step 9 - Lighting.
/// </summary>
public static partial class FarmLandBuilder
{
    // Blur from camera movement only, which needs no motion vectors from every object. Intensity scales the
    // blur against the camera's shutter, and the clamp caps the streak at a share of the screen, so fast
    // flight with Shift held doesn't smear the whole view.
    const float CameraMotionBlurIntensity = 0.35f;
    const float CameraMotionBlurClamp = 0.05f;

    [MenuItem("Farm Land/Step 13 - Camera Feel")]
    public static void BuildCameraFeel()
    {
        BuildLighting();
    }

    static void AddCameraMotionBlur(VolumeProfile profile)
    {
        var blur = AddVolumeComponent<MotionBlur>(profile);
        blur.mode.Override(MotionBlurMode.CameraOnly);
        blur.quality.Override(MotionBlurQuality.Medium);
        blur.intensity.Override(CameraMotionBlurIntensity);
        blur.clamp.Override(CameraMotionBlurClamp);
    }
}
