using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonChefController : MonoBehaviour
{
    [Header("Рух (Movement)")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundGravity = -2f;

    [Header("Огляд (Look)")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController _characterController;
    private float _cameraPitch = 0f;
    private float _verticalVelocity = 0f;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        // Блокуємо курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        // Читаємо стандартні осі миші
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Горизонтальний поворот персонажа
        transform.Rotate(Vector3.up * mouseX);

        // Вертикальний нахил камери
        _cameraPitch -= mouseY;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -maxLookAngle, maxLookAngle);
        playerCamera.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        // Читаємо стандартні кнопки WASD
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 moveDirection = (transform.right * moveX) + (transform.forward * moveZ);

        if (_characterController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = groundGravity;
        }
        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = _verticalVelocity;

        _characterController.Move(velocity * Time.deltaTime);
    }
}