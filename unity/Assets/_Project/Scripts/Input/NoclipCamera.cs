using UnityEngine;
using UnityEngine.InputSystem;

namespace Fpv450.Input
{
    /// <summary>
    /// Free-flying review camera (PRD-07): mouse look, WASD to move, Shift for fast,
    /// E or Space up, Q or Ctrl down, and the mouse wheel to scale the speed.
    /// A stub that reads the mouse and keyboard directly; the TX12 layer comes in phase 6.
    /// </summary>
    public sealed class NoclipCamera : MonoBehaviour
    {
        // All assumed (design.md, decision 7): the gallery spans up to a few hundred metres.
        const float StartSpeed = 5f;            // m/s
        const float FastFactor = 4f;            // while Shift is held
        const float WheelFactor = 1.25f;        // per wheel notch
        const float MinSpeed = 0.5f;            // m/s
        const float MaxSpeed = 200f;            // m/s
        const float LookDegreesPerCount = 0.1f; // mouse look sensitivity
        const float PitchLimit = 89f;           // degrees

        float speed = StartSpeed;
        float yaw;
        float pitch;

        void OnEnable()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = Mathf.DeltaAngle(0f, euler.x);
            Cursor.lockState = CursorLockMode.Locked;
        }

        void OnDisable() => Cursor.lockState = CursorLockMode.None;

        /// <summary>
        /// Puts the camera at <paramref name="position"/>, turned <paramref name="yawDeg"/> about up and pitched
        /// <paramref name="pitchDeg"/> down (a saved viewpoint); mouse look and movement continue from that pose.
        /// </summary>
        public void MoveTo(Vector3 position, float yawDeg, float pitchDeg)
        {
            yaw = yawDeg;
            pitch = Mathf.Clamp(pitchDeg, -PitchLimit, PitchLimit);
            transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
        }

        void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue();
                yaw += look.x * LookDegreesPerCount;
                pitch = Mathf.Clamp(pitch - look.y * LookDegreesPerCount, -PitchLimit, PitchLimit);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

                // One notch per frame with wheel motion, because the units per notch differ by platform.
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0f)
                    speed = Mathf.Clamp(wheel > 0f ? speed * WheelFactor : speed / WheelFactor, MinSpeed, MaxSpeed);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            var input = new Vector3(
                Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed),
                Axis(keyboard.eKey.isPressed || keyboard.spaceKey.isPressed, keyboard.qKey.isPressed || keyboard.ctrlKey.isPressed),
                Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));
            float current = keyboard.shiftKey.isPressed ? speed * FastFactor : speed;
            transform.position = Step(transform.position, transform.rotation, input, current, Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Moves for <paramref name="dt"/> seconds at <paramref name="speed"/> m/s. Input x is right and
        /// z is forward along the view, y is world up; each runs from -1 to 1, and diagonals are no faster.
        /// </summary>
        public static Vector3 Step(Vector3 position, Quaternion view, Vector3 input, float speed, float dt)
        {
            Vector3 direction = view * new Vector3(input.x, 0f, input.z) + Vector3.up * input.y;
            return position + Vector3.ClampMagnitude(direction, 1f) * (speed * dt);
        }

        static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
