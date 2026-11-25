using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonChefController : MonoBehaviour
{
    [Header("Рух (Movement)")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundGravity = -2f;

    [Header("Відчуття руху")]
    [Tooltip("Як швидко шеф розганяється до повної швидкості, м/с²")]
    [SerializeField] private float acceleration = 45f;
    [Tooltip("Як швидко зупиняється, коли клавіші відпущено, м/с²")]
    [SerializeField] private float deceleration = 60f;
    [Tooltip("Відстань між кроками, м. 0 = без звуку кроків")]
    [SerializeField] private float stepDistance = 1.7f;

    [Header("Огляд (Look)")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController _characterController;
    private float _cameraPitch = 0f;
    private float _verticalVelocity = 0f;
    private Vector3 _planarVelocity = Vector3.zero;
    private float _stepAccumulator = 0f;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        // Блокуємо курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!GameFlow.CanMove)
        {
            // в ноутбуці й на результаті гравець стоїть, інерція не переноситься в наступний кадр руху
            _planarVelocity = Vector3.zero;
            return;
        }

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
        // Сирі осі + власний розгін: відгук без «ковзання» вбудованого згладжування
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // ClampMagnitude: по діагоналі шеф більше не біжить у 1.41 раза швидше
        Vector3 wish = Vector3.ClampMagnitude((transform.right * moveX) + (transform.forward * moveZ), 1f) * moveSpeed;

        float rate = wish.sqrMagnitude > 0.01f ? acceleration : deceleration;
        _planarVelocity = Vector3.MoveTowards(_planarVelocity, wish, rate * Time.deltaTime);

        if (_characterController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = groundGravity;
        }
        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = _planarVelocity;
        velocity.y = _verticalVelocity;

        _characterController.Move(velocity * Time.deltaTime);

        HandleFootsteps();
    }

    private void HandleFootsteps()
    {
        if (stepDistance <= 0f) return;

        float speed = _planarVelocity.magnitude;
        if (!_characterController.isGrounded || speed < 0.5f)
        {
            _stepAccumulator = 0f;
            return;
        }

        _stepAccumulator += speed * Time.deltaTime;
        if (_stepAccumulator >= stepDistance)
        {
            _stepAccumulator = 0f;
            Feedback.Sfx("step", 0.5f, Random.Range(0.85f, 1.1f));
        }
    }
}