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

    [Header("Фідбек")]
    [SerializeField] float popTime = 0.35f;       // поява: «вистрибує» з нуля
    [SerializeField] float reactTime = 0.5f;      // підстрибування і колір при реакції
    [SerializeField] float urgentBelow = 0.25f;   // частка терпіння, нижче якої текст пульсує

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

    string _speciesId;
    string _orderString = "";

    Vector3 _baseScale = Vector3.one;
    Vector3 _textBaseScale = Vector3.one;
    float _age;
    float _phase;
    float _reactT;
    float _textRestoreT;
    bool _moving;
    bool _urgent;
    Color _flashColor = Color.white;

    void Awake()
    {
        _baseScale = transform.localScale;
        if (orderText != null) _textBaseScale = orderText.transform.localScale;
        _phase = Random.value * 6.28f;
    }

    public void Init(Order order, Transform chef, SpeciesDefinition species)
    {
        _chef = chef;
        _cam = Camera.main != null ? Camera.main.transform : null;
        _speciesId = species.id;

        moveSpeed = species.moveSpeed;

        if (orderText != null)
        {
            string badgeColor = species.id switch
            {
                "vip" => "#FFD700",   // золотий
                "grump" => "#FF7043", // коралово-червоний
                _ => "#4FC3F7"        // блакитний
            };

            _orderString =
                $"<color={badgeColor}><b>[{species.displayName.ToUpper()}]</b></color>\n" +
                string.Join("\n", order.required);

            orderText.text = _orderString;
        }

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
        _urgent = fraction < urgentBelow;

        // Під час реакції колір задає реакція, а не терпіння
        if (_reactT > 0f) return;
        if (body != null) body.material.color = Color.Lerp(Color.red, Color.green, fraction);
    }

    /// <summary>Видима реакція клієнта: підстрибує, спалахує кольором, показує репліку.</summary>
    public void React(string line, Color color, float showSeconds = 2f)
    {
        _flashColor = color;
        _reactT = reactTime;

        if (orderText != null)
        {
            orderText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{line}</b></color>";
            _textRestoreT = showSeconds;
        }
    }

    public void Leave(Vector3 exit, bool satisfied, string line = null)
    {
        _leaving = true;
        _exit = exit;
        _urgent = false;
        _textRestoreT = 0f;

        // Піде з кольором, що показує настрій
        _flashColor = satisfied ? new Color(0.45f, 0.95f, 0.5f) : new Color(0.95f, 0.4f, 0.35f);
        _reactT = reactTime;

        if (orderText != null)
            orderText.text = line ?? (satisfied ? "дякую!" : "...");
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;
        _moving = false;

        if (_reactT > 0f)
        {
            _reactT -= dt;
            if (body != null) body.material.color = _flashColor;
        }

        if (_textRestoreT > 0f)
        {
            _textRestoreT -= dt;
            if (_textRestoreT <= 0f && orderText != null && !_leaving)
                orderText.text = _orderString;
        }

        Move();
        ApplyScale();
    }

    void Move()
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

    // Поява (пружинить), підстрибування при реакції, похитування під час ходьби
    void ApplyScale()
    {
        float pop = _age < popTime ? EaseOutBack(_age / popTime) : 1f;

        float punch = 1f;
        if (_reactT > 0f)
        {
            float u = 1f - Mathf.Clamp01(_reactT / reactTime);
            punch = 1f + 0.22f * Mathf.Sin(u * Mathf.PI);
        }

        float bob = _moving ? Mathf.Sin(Time.time * 12f + _phase) * 0.04f : 0f;

        float k = pop * punch;
        transform.localScale = new Vector3(
            _baseScale.x * k * (1f - bob * 0.5f),
            _baseScale.y * k * (1f + bob),
            _baseScale.z * k * (1f - bob * 0.5f));

        // Нетерплячий клієнт: текст пульсує
        if (orderText != null)
        {
            float pulse = _urgent ? 1f + 0.12f * Mathf.Sin(Time.time * 9f) : 1f;
            orderText.transform.localScale = _textBaseScale * pulse;
        }
    }

    static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
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
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(before, target, speed * Time.deltaTime);
        _goal = target;

        if ((transform.position - before).sqrMagnitude > 1e-8f) _moving = true;
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