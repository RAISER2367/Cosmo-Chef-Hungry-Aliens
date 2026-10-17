using TMPro;
using UnityEngine;

public class Alien : MonoBehaviour
{
    [Header("Рух")]
    [SerializeField] float moveSpeed = 1.5f;
    [SerializeField] float turnSpeed = 6f;

    [Header("Очікування")]
    [SerializeField] float wanderRadius = 0.4f;   // як далеко ходить навколо свого місця
    [SerializeField] float wanderSpeed = 0.5f;    // множник швидкості при нервовій ходьбі
    [SerializeField] float pauseMin = 0.8f;
    [SerializeField] float pauseMax = 2.5f;

    [Header("Посилання")]
    [SerializeField] Renderer body;
    [SerializeField] TMP_Text orderText;

    Transform _chef;
    Transform _cam;
    Vector3 _spot;        // місце в черзі
    Vector3 _wander;      // поточна точка для нервової ходьби
    Vector3 _goal;        // куди зараз рухаємось (для повороту)
    Vector3 _exit;
    float _pause;
    float _lookYaw;
    bool _front;
    bool _leaving;

    public void Init(Order order, Transform chef)
    {
        _chef = chef;
        _cam = Camera.main != null ? Camera.main.transform : null;

        if (orderText != null) orderText.text = string.Join("\n", order.required);

        _spot = _goal = transform.position;
        _wander = PickWander();
    }

    public void MoveTo(Vector3 point)
    {
        _spot = point;
        _wander = PickWander();
    }

    public void SetFront(bool front) => _front = front;

    public void SetPatience(float fraction)
    {
        if (body != null) body.material.color = Color.Lerp(Color.red, Color.green, fraction);
    }

    public void Leave(Vector3 exit, bool satisfied)
    {
        _leaving = true;
        _exit = exit;
        if (orderText != null) orderText.text = satisfied ? "дякую!" : "...";
    }

    void Update()
    {
        if (_leaving)
        {
            Walk(_exit, moveSpeed);
            if (FlatNear(_exit)) Destroy(gameObject);
            return;
        }

        // Перший у черзі: стоїть на місці і дивиться на Chef
        if (_front)
        {
            Walk(_spot, moveSpeed);
            if (FlatNear(_spot)) TurnFlat(_chef.position);
            else TurnFlat(_goal);
            return;
        }

        // Очікування: стоїть, озирається, потім нервово ходить навколо місця
        if (_pause > 0f)
        {
            _pause -= Time.deltaTime;
            TurnFlatYaw(_lookYaw);
            return;
        }

        if (FlatNear(_wander))
        {
            _pause = Random.Range(pauseMin, pauseMax);
            _lookYaw = Random.Range(0f, 360f);
            _wander = PickWander();
            return;
        }

        Walk(_wander, moveSpeed * wanderSpeed);
        TurnFlat(_goal);
    }

    void LateUpdate()
    {
        // Текст завжди дивиться на камеру, незалежно від повороту клієнта
        if (orderText == null || _cam == null) return;

        Vector3 dir = orderText.transform.position - _cam.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            orderText.transform.rotation = Quaternion.LookRotation(dir);
    }

    Vector3 PickWander()
    {
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        return _spot + new Vector3(r.x, 0f, r.y);
    }

    void Walk(Vector3 target, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        _goal = target;
    }

    void TurnFlat(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    void TurnFlatYaw(float yaw)
    {
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.Euler(0f, yaw, 0f), turnSpeed * Time.deltaTime);
    }

    bool FlatNear(Vector3 p)
    {
        Vector3 a = transform.position;
        float dx = a.x - p.x, dz = a.z - p.z;
        return dx * dx + dz * dz < 0.0025f; // 5 см
    }
}