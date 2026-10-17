using Fpv450.Input;
using NUnit.Framework;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    public sealed class NoclipCameraTests
    {
        const float Speed = 20f; // m/s, any value

        // Spec: 1 s of simulated time at 30 fps and at 144 fps with the same input agree within 1 %.
        [Test]
        public void Step_DistanceIsFrameRateIndependent()
        {
            Quaternion view = Quaternion.Euler(-20f, 35f, 0f);
            var input = new Vector3(1f, 1f, 1f);

            float at30 = DistanceAfterOneSecond(30, view, input);
            float at144 = DistanceAfterOneSecond(144, view, input);

            Assert.That(at30, Is.EqualTo(Speed).Within(1).Percent);
            Assert.That(at144, Is.EqualTo(at30).Within(1).Percent);
        }

        static float DistanceAfterOneSecond(int fps, Quaternion view, Vector3 input)
        {
            Vector3 position = Vector3.zero;
            for (int frame = 0; frame < fps; frame++)
                position = NoclipCamera.Step(position, view, input, Speed, 1f / fps);
            return position.magnitude;
        }
    }
}
