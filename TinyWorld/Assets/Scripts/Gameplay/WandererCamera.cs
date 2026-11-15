using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace TinyWorld
{
    [DefaultExecutionOrder(-50)]
    public sealed class WandererCamera : MonoBehaviour
    {
        public WandererMotor player;
        public CinemachineCamera rig;
        public float sensitivity = .11f;
        public bool invertY;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 13;
        float eye = 1.25f;
        void Awake() { Yaw = transform.eulerAngles.y; }
        void Update()
        {
            if (player.ControlsEnabled && Time.timeScale > 0 && Application.isFocused)
            {
                Vector2 delta = Mouse.current != null && Cursor.lockState == CursorLockMode.Locked ? Mouse.current.delta.ReadValue() * sensitivity : Vector2.zero;
                if (Gamepad.current != null) delta += Gamepad.current.rightStick.ReadValue() * (120 * Time.unscaledDeltaTime);
                Yaw += delta.x;
                Pitch = Mathf.Clamp(Pitch + delta.y * (invertY ? 1 : -1), -70, 62);
            }
            eye = Mathf.Lerp(eye, player.Crouched || player.Action == "Roll" ? .8f : 1.25f, 1 - Mathf.Exp(-10 * Time.deltaTime));
            Vector3 target = player.transform.position + Vector3.up * eye;
            Vector3 deltaPosition = target - transform.position;
            if (deltaPosition.sqrMagnitude > 64) rig.OnTargetObjectWarped(transform, deltaPosition);
            transform.SetPositionAndRotation(target, Quaternion.Euler(Pitch, Yaw, 0));
            float fov = player.ControlsEnabled && (player.Speed > 8 || player.Swinging) ? 72 : 64;
            rig.Lens.FieldOfView = Mathf.Lerp(rig.Lens.FieldOfView, fov, 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime));
        }
        public void SetOrbit(float yaw, float pitch) { Yaw = yaw; Pitch = Mathf.Clamp(pitch, -70, 62); }
    }
}
