using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class BalanceHUD : MonoBehaviour
{
    [SerializeField] float flashTime = 0.5f;
    [SerializeField] float punchScale = 0.15f;

    static readonly Color GoodColor = new Color(0.5f, 1f, 0.55f);
    static readonly Color BadColor = new Color(1f, 0.45f, 0.4f);

    TMP_Text _text;
    Color _baseColor;
    Vector3 _baseScale;

    int _lastCredits;
    int _lastDebt;
    bool _initialized;

    float _flash;
    Color _flashColor;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _baseColor = _text.color;
        _baseScale = transform.localScale;
    }

    void OnEnable()
    {
        GameSession.OnBalanceChanged += Refresh;
        _initialized = false;
        Refresh();
    }

    void OnDisable()
    {
        GameSession.OnBalanceChanged -= Refresh;
        _text.color = _baseColor;
        transform.localScale = _baseScale;
    }

    void Refresh()
    {
        int credits = GameSession.Credits;
        int debt = GameSession.Debt;

        _text.text = $"Кредити: {credits}\nБорг: {debt}";

        if (_initialized)
        {
            // Борг, що виріс, важливіший за зміну кредитів: червоний має пріоритет
            if (debt > _lastDebt) Flash(BadColor);
            else if (debt < _lastDebt || credits > _lastCredits) Flash(GoodColor);
            else if (credits < _lastCredits) Flash(BadColor);
        }

        _lastCredits = credits;
        _lastDebt = debt;
        _initialized = true;
    }

    void Flash(Color color)
    {
        _flashColor = color;
        _flash = flashTime;
    }

    void Update()
    {
        if (_flash <= 0f) return;

        _flash -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_flash / flashTime);

        _text.color = Color.Lerp(_baseColor, _flashColor, t);
        transform.localScale = _baseScale * (1f + punchScale * t);
    }
}