using System.Collections;
using UnityEngine;

public class FoodVisual : MonoBehaviour
{
    [Header("М'ясо / за замовчуванням")]
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Material rawMat;
    [SerializeField] Material cutMat;
    [SerializeField] Material readyMat;
    [SerializeField] Material burntMat;

    [Header("Огірок")]
    [Tooltip("ПРЕФАБ огірка, не об'єкт зі сцени")]
    [SerializeField] GameObject cucumberPrefab;
    [SerializeField] Material vegRaw;
    [SerializeField] Material vegCut;

    [Header("Нарізання")]
    [SerializeField] Vector3 vegShrinkAxes = new Vector3(0f, 1f, 0f); // Для сплющення по вертикалі має бути Y:1
    [SerializeField] float minScaleFactor = 0.5f;
    [SerializeField] float punchAmount = 0.15f;

    GameObject _cucumber;
    Renderer[] _vegRenderers = new Renderer[0];
    Vector3 _baseScale;
    Coroutine _punch;
    bool _isVeg;
    bool _initialized;

    // Зберігаємо пам'ять про останній об'єкт, щоб не скидати розмір щокадру
    int _lastFoodId = -1;
    FoodState _lastFoodState = FoodState.Raw;

    void Awake()
    {
        InitIfNeeded();
    }

    void InitIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        _baseScale = transform.localScale;

        if (cucumberPrefab != null)
        {
            _cucumber = Instantiate(cucumberPrefab, transform);
            _cucumber.transform.localPosition = Vector3.zero;
            _cucumber.SetActive(false);
            _vegRenderers = _cucumber.GetComponentsInChildren<Renderer>(true);
        }
    }

    public void Apply(FoodItem food)
    {
        InitIfNeeded();

        if (food == null)
        {
            gameObject.SetActive(false);
            _lastFoodId = -1; // Скидаємо пам'ять, коли руки чи стіл порожні
            return;
        }

        gameObject.SetActive(true);

        _isVeg = food.foodName == "cucumber" && _cucumber != null;

        if (_cucumber != null) _cucumber.SetActive(_isVeg);
        if (targetRenderer != null) targetRenderer.enabled = !_isVeg;

        Material m = PickMaterial(food.state, _isVeg);
        if (m != null)
        {
            if (_isVeg)
            {
                foreach (var r in _vegRenderers) r.sharedMaterial = m;
            }
            else if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = m;
            }
        }

        // ВАЖЛИВО: Оновлюємо розмір ТІЛЬКИ якщо це новий продукт, або його статус змінився на Cut/Ready
        if (_lastFoodId != food.id || _lastFoodState != food.state)
        {
            bool isCut = food.state == FoodState.Cut;
            SetCutProgress(isCut ? 1f : 0f, false);

            _lastFoodId = food.id;
            _lastFoodState = food.state;
        }
    }

    Material PickMaterial(FoodState state, bool veg)
    {
        switch (state)
        {
            case FoodState.Raw: return veg ? (vegRaw != null ? vegRaw : rawMat) : rawMat;
            case FoodState.Cut: return veg ? (vegCut != null ? vegCut : cutMat) : cutMat;
            case FoodState.Ready: return readyMat;
            case FoodState.Burnt: return burntMat;
        }
        return null;
    }

    public void SetCutProgress(float progress, bool punch)
    {
        InitIfNeeded();

        float s = Mathf.Lerp(1f, minScaleFactor, Mathf.Clamp01(progress));
        Vector3 axes = _isVeg ? vegShrinkAxes : Vector3.one;

        Vector3 factor = new Vector3(
            Mathf.Lerp(1f, s, axes.x),
            Mathf.Lerp(1f, s, axes.y),
            Mathf.Lerp(1f, s, axes.z));

        transform.localScale = Vector3.Scale(_baseScale, factor);

        if (!punch || !gameObject.activeInHierarchy) return;

        if (_punch != null) StopCoroutine(_punch);
        _punch = StartCoroutine(Punch());
    }

    IEnumerator Punch()
    {
        Vector3 rest = transform.localScale;
        Vector3 squash = rest * (1f - punchAmount);
        const float half = 0.06f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(rest, squash, t / half);
            yield return null;
        }

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(squash, rest, t / half);
            yield return null;
        }

        transform.localScale = rest;
        _punch = null;
    }
}