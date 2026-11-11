using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Review camera. Hold the right mouse button to look, WASD to move, Q/E to go down/up,
/// Shift to go faster, and scroll to change the base speed. The view tilts a little into strafes and
/// turns and eases back level, the way many first-person games bank their camera.
/// </summary>
public class FreeFlyCamera : MonoBehaviour
{
    [SerializeField] float moveSpeed = 25f;
    [SerializeField] float fastMultiplier = 4f;
    [SerializeField] float lookSensitivity = 0.15f;
    [SerializeField] float minMoveSpeed = 2f;
    [SerializeField] float maxMoveSpeed = 200f;

    [Header("Tilt")]
    [Tooltip("Roll in degrees while strafing.")]
    [SerializeField] float strafeTilt = 2.5f;
    [Tooltip("Roll in degrees per 100 degrees a second of turning.")]
    [SerializeField] float turnTilt = 1.5f;
    [Tooltip("Most roll in degrees from both together.")]
    [SerializeField] float maxTilt = 4f;
    [Tooltip("Seconds for the tilt to settle.")]
    [SerializeField] float tiltSmoothTime = 0.18f;

    float yaw;
    float pitch;
    float roll;
    float rollVelocity;
    float strafe;
    float strafeVelocity;
    float turnRate;
    Quaternion appliedRotation;

    void OnEnable()
    {
        ReadRotation();
    }

    void OnDisable()
    {
        SetLooking(false);
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || keyboard == null)
            return;

        // Something else, such as a review script, moved the camera, so carry on from there.
        if (transform.rotation != appliedRotation)
            ReadRotation();

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        bool looking = mouse.rightButton.isPressed;
        SetLooking(looking);
        float yawChange = 0f;
        if (looking)
        {
            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            yawChange = delta.x;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (!Mathf.Approximately(scroll, 0f))
            moveSpeed = Mathf.Clamp(moveSpeed * (scroll > 0f ? 1.15f : 1f / 1.15f), minMoveSpeed, maxMoveSpeed);

        // Movement follows the view without its roll, so tilting never steers the camera.
        Quaternion level = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 forward = level * Vector3.forward;
        Vector3 right = level * Vector3.right;
        Vector3 direction = Vector3.zero;
        float sideways = 0f;
        if (keyboard.wKey.isPressed) direction += forward;
        if (keyboard.sKey.isPressed) direction -= forward;
        if (keyboard.dKey.isPressed) { direction += right; sideways += 1f; }
        if (keyboard.aKey.isPressed) { direction -= right; sideways -= 1f; }
        if (keyboard.eKey.isPressed) direction += Vector3.up;
        if (keyboard.qKey.isPressed) direction -= Vector3.up;

        float speed = keyboard.leftShiftKey.isPressed ? moveSpeed * fastMultiplier : moveSpeed;
        transform.position += Vector3.ClampMagnitude(direction, 1f) * (speed * deltaTime);

        // Positive roll in Unity tips the view's top to the left, so leaning right is negative.
        strafe = Mathf.SmoothDamp(strafe, sideways, ref strafeVelocity, tiltSmoothTime, Mathf.Infinity, deltaTime);
        turnRate = Mathf.Lerp(turnRate, yawChange / deltaTime, 1f - Mathf.Exp(-deltaTime / tiltSmoothTime));
        float targetRoll = Mathf.Clamp(-strafe * strafeTilt - turnRate * 0.01f * turnTilt, -maxTilt, maxTilt);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, tiltSmoothTime, Mathf.Infinity, deltaTime);

        appliedRotation = Quaternion.Euler(pitch, yaw, roll);
        transform.rotation = appliedRotation;
    }

    void ReadRotation()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = Mathf.DeltaAngle(0f, euler.x);
        roll = Mathf.DeltaAngle(0f, euler.z);
        rollVelocity = 0f;
        appliedRotation = transform.rotation;
    }

    static void SetLooking(bool looking)
    {
        Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !looking;
    }
}
