using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ChefMovement : MonoBehaviour
{
    [Header("Швидкості (м/с)")]
    [SerializeField] float maxSpeed = 4f;
    [SerializeField] float acceleration = 25f;   // як швидко розганяється
    [SerializeField] float deceleration = 30f;   // як швидко гальмує

    [Header("Поворот")]
    [SerializeField] float turnSpeed = 14f;      // чим більше, тим швидше розвертається

    [Header("Гравітація")]
    [SerializeField] float gravity = -20f;

    CharacterController cc;
    Vector3 velocity;        // горизонтальна швидкість
    float verticalVelocity;

    void Awake() => cc = GetComponent<CharacterController>();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 raw = Vector2.zero;
        if (kb.wKey.isPressed) raw.y += 1;
        if (kb.sKey.isPressed) raw.y -= 1;
        if (kb.dKey.isPressed) raw.x += 1;
        if (kb.aKey.isPressed) raw.x -= 1;
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        Vector3 wish = new Vector3(raw.x, 0, raw.y) * maxSpeed;

        // Плавний розгін і гальмування замість миттєвої зміни швидкості
        float rate = wish.sqrMagnitude > 0.01f ? acceleration : deceleration;
        velocity = Vector3.MoveTowards(velocity, wish, rate * Time.deltaTime);

        // Плавний поворот у напрямку руху
        if (velocity.sqrMagnitude > 0.05f)
        {
            var targetRot = Quaternion.LookRotation(velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // Гравітація
        verticalVelocity = cc.isGrounded ? -1f : verticalVelocity + gravity * Time.deltaTime;

        Vector3 motion = velocity;
        motion.y = verticalVelocity;
        cc.Move(motion * Time.deltaTime);
    }
}