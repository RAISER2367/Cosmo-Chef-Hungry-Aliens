using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [SerializeField] Transform body;          // Chef, щоб повертати його по горизонталі
    [SerializeField] float sensitivity = 0.1f;
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 80f;

    float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue() * sensitivity;

        // Горизонталь: крутимо тіло шефа
        body.Rotate(Vector3.up * delta.x);

        // Вертикаль: нахиляємо саму камеру з обмеженням
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void OnApplicationFocus(bool focus)
    {
        Cursor.lockState = focus ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !focus;
    }
}