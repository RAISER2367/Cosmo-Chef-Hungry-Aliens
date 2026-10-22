using UnityEngine;

public class TurbulenceDirector : MonoBehaviour
{
    public static TurbulenceDirector Instance { get; private set; }

    [Header("Ймовірність бурі на зміну")]
    [Range(0f, 1f)][SerializeField] float stormChance = 0.35f;

    [Header("Коли може початися (частка від тривалості зміни)")]
    [Range(0f, 1f)][SerializeField] float earliestStart = 0.1f;
    [Range(0f, 1f)][SerializeField] float latestStart = 0.7f;

    [Header("Тривалість бурі, с")]
    [SerializeField] float minDuration = 20f;
    [SerializeField] float maxDuration = 60f;

    [Header("Сила, м/с")]
    [SerializeField] float minStrength = 0.3f;     // легке похитування
    [SerializeField] float maxStrength = 2.5f;     // сильний знос
    [SerializeField] float levelChangeInterval = 3f;

    // Для майбутнього "детектора": показує ймовірність, а не результат зміни
    public float Chance => stormChance;
    public string ForecastLabel => stormChance < 0.2f ? "низька" : stormChance < 0.5f ? "помірна" : "висока";

    public bool Active { get; private set; }
    public float CurrentStrength { get; private set; } // м/с, з огинаючою
    public float Shake { get; private set; }            // 0..1, для камери

    bool _rolled;
    bool _planned;
    float _elapsed;
    float _stormStart;
    float _stormEnd;
    float _level;
    float _targetLevel;
    float _levelTimer;

    void Awake() => Instance = this;

    void Update()
    {
        if (GameFlow.Phase != GamePhase.Shift)
        {
            ResetState();
            return;
        }

        if (!_rolled) Roll();

        _elapsed += Time.deltaTime;
        bool now = _planned && _elapsed >= _stormStart && _elapsed < _stormEnd;

        if (now != Active)
        {
            Active = now;
            Debug.Log(Active ? "[Storm] буря почалась" : "[Storm] буря закінчилась");
        }

        if (Active) UpdateStorm();
        else { CurrentStrength = 0f; Shake = 0f; }
    }

    void Roll()
    {
        _rolled = true;
        _planned = Random.value < stormChance;

        if (!_planned)
        {
            Debug.Log("[Storm] цієї зміни буря не очікується");
            return;
        }

        float dur = ShiftSettings.Duration;
        _stormStart = Random.Range(earliestStart, latestStart) * dur;
        _stormEnd = Mathf.Min(dur, _stormStart + Random.Range(minDuration, maxDuration));
        _level = Random.value;
        _targetLevel = _level;
        _levelTimer = 0f;

        // Лише для тесту: після перевірки видаліть, щоб гравець не бачив заздалегідь
        Debug.Log($"[Storm] (тест) буря з {_stormStart:F0}с до {_stormEnd:F0}с");
    }

    void UpdateStorm()
    {
        float dt = Time.deltaTime;

        // Інтенсивність час від часу змінюється: від легкої до сильної
        _levelTimer -= dt;
        if (_levelTimer <= 0f)
        {
            _targetLevel = Random.value;
            _levelTimer = levelChangeInterval * Random.Range(0.6f, 1.4f);
        }
        _level = Mathf.MoveTowards(_level, _targetLevel, dt * 0.4f);

        // Плавний вхід і вихід з бурі
        float p = Mathf.InverseLerp(_stormStart, _stormEnd, _elapsed);
        float envelope = Mathf.Sin(p * Mathf.PI);

        Shake = _level * envelope;
        CurrentStrength = Mathf.Lerp(minStrength, maxStrength, _level) * envelope;
    }

    void ResetState()
    {
        if (Active) Debug.Log("[Storm] зміну завершено");

        Active = false;
        CurrentStrength = 0f;
        Shake = 0f;
        _rolled = false;
        _planned = false;
        _elapsed = 0f;
    }
}