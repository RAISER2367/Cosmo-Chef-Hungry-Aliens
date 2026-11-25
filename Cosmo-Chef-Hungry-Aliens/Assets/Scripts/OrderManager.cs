using System.Collections.Generic;
using UnityEngine;

public class Order
{
    public readonly List<string> required = new List<string>();
    public float patience;
    public float maxPatience;
    public int reward;
    public Alien alien;
    public string speciesId;
    public bool warned;   // чи вже пролунало попередження про закінчення терпіння

    public override string ToString() => $"[{speciesId}: {string.Join(", ", required)}]";
}

public class OrderManager : MonoBehaviour
{
    [Header("Економіка")]
    [SerializeField] float markupMultiplier = 3.5f; // нагорода = собівартість інгредієнтів * це число

    int _interestDaysApplied;
    public static OrderManager Instance { get; private set; }

    [Header("Клієнти")]
    [SerializeField] Alien alienPrefab;
    [SerializeField] Transform spawnPoint;          // де з'являються, поза трейлером
    [SerializeField] Transform exitPoint;           // куди ідуть після обслуговування
    [SerializeField] Transform[] queueSpots;        // 0 = найближче до вікна
    [SerializeField] float firstClientDelay = 3f;   // пауза перед першим клієнтом, щоб гравець встиг озирнутись
    [SerializeField] float patienceSeconds = 40f;

    [Header("Замовлення")]
    [SerializeField] string[] ingredientPool = { "meat", "cucumber" };
    [SerializeField] int maxIngredients = 3;        // як plateCapacity
    [SerializeField] int rewardPerIngredient = 10;

    [SerializeField] Transform chef;

    readonly List<Order> _queue = new List<Order>();
    readonly Queue<float> _timerMilestones = new Queue<float>(); // позначки для дебаг-таймера

    float _shiftLeft;
    float _spawnTimer;
    bool _running;

    public int Served { get; private set; }
    public int Lost { get; private set; }
    public int Credits { get; private set; }

    // Для EventDirector та інших систем
    public float ShiftLeft => _shiftLeft;
    public bool Running => _running;

    [Header("Хвиля спавну")]
    [SerializeField] float baseInterval = 14f;
    [SerializeField] float peakInterval = 6f;
    [SerializeField] AnimationCurve wave = AnimationCurve.EaseInOut(0, 0, 1, 1); // підняти пік у редакторі ~0.5

    [Header("Реакції клієнтів")]
    [SerializeField] float happyFactor = 1.2f;      // payFactor від цього значення = «Смакота!»
    [SerializeField] float okFactor = 0.95f;        // нижче цього = «Таке собі…»

    static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    static readonly Color Green = new Color(0.5f, 1f, 0.55f);
    static readonly Color Cyan = new Color(0.45f, 0.85f, 1f);
    static readonly Color Orange = new Color(1f, 0.65f, 0.3f);
    static readonly Color Red = new Color(1f, 0.4f, 0.35f);

    float CurrentSpawnInterval()
    {
        float dayProgress = Mathf.Repeat(
            (ShiftSettings.Duration - _shiftLeft) / ShiftSettings.SecondsPerDay, 1f);

        // Трикутна хвиля: 0 на початку/кінці дня, 1 на середині
        float intensity = 1f - Mathf.Abs(dayProgress - 0.5f) * 2f;
        intensity = wave.Evaluate(intensity);

        int dayIndex = Mathf.FloorToInt((ShiftSettings.Duration - _shiftLeft) / ShiftSettings.SecondsPerDay);
        float dayHardening = 1f - dayIndex * 0.08f; // кожен наступний день трохи щільніше

        return Mathf.Lerp(baseInterval, peakInterval, intensity) * Mathf.Max(0.5f, dayHardening);
    }

    void Awake()
    {
        Instance = this;
    }

    public void StartShift()
    {
        float duration = ShiftSettings.Duration;

        _shiftLeft = duration;
        _spawnTimer = firstClientDelay;
        _running = true;
        _interestDaysApplied = 0;

        Served = 0;
        Lost = 0;
        Credits = 0;

        GameSession.BeginShiftAccounting();
        BuildTimerMilestones(duration);

        foreach (Station s in FindObjectsByType<Station>(FindObjectsSortMode.None))
            s.ClearContents();

        ChefInteraction chefInteraction = FindFirstObjectByType<ChefInteraction>();
        if (chefInteraction != null) chefInteraction.ResetHand();

        Feedback.Sfx("start");
        Feedback.Good("Зміна почалась!");
    }

    void BuildTimerMilestones(float duration)
    {
        _timerMilestones.Clear();

        // Хвилинні позначки (як і раніше)
        float t = duration;
        while (t > 30f)
        {
            t -= 60f;
            if (t > 30f) _timerMilestones.Enqueue(t);
        }

        // Фінальні секунди
        _timerMilestones.Enqueue(30f);
        _timerMilestones.Enqueue(15f);
        _timerMilestones.Enqueue(10f);
        _timerMilestones.Enqueue(5f);
        _timerMilestones.Enqueue(2f);
    }

    void Update()
    {
        if (!_running) return;

        _shiftLeft -= Time.deltaTime;
        float elapsed = ShiftSettings.Duration - _shiftLeft;
        int dayIndex = Mathf.FloorToInt(elapsed / ShiftSettings.SecondsPerDay);

        // Відсоток тепер береться з GameSession: 12%, а після реструктуризації 6%
        while (_interestDaysApplied < dayIndex && _interestDaysApplied < ShiftSettings.Days)
        {
            _interestDaysApplied++;
            if (ShiftSettings.Days > 1) GameSession.ApplyDailyInterest(GameSession.DebtInterestRate);
        }

        // Дебаг-таймер: while, щоб не пропустити кілька позначок за один кадр
        while (_timerMilestones.Count > 0 && _shiftLeft <= _timerMilestones.Peek())
        {
            float mark = _timerMilestones.Dequeue();
            if (mark >= 60f)
                Debug.Log($"[Таймер] Залишилося: {mark / 60f} хв");
            else
                Debug.Log($"[Таймер] Залишилося: {mark} сек!");

            // Те, що було лише в консолі, тепер бачить гравець
            if (mark <= 30f)
            {
                Feedback.Toast($"До кінця зміни {mark:0} с", Orange);
                Feedback.Sfx("warn", 0.6f);
            }
        }

        if (_shiftLeft <= 0f)
        {
            EndShift();
            return;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f && _queue.Count < queueSpots.Length)
        {
            SpawnOrder();
            _spawnTimer = CurrentSpawnInterval();
        }

        // Йдемо з кінця, щоб видалення не зсувало індекси
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            Order o = _queue[i];
            o.patience -= Time.deltaTime;

            float fraction = o.patience / o.maxPatience;
            o.alien.SetPatience(fraction);

            if (!o.warned && fraction < 0.25f && o.patience > 0f)
            {
                o.warned = true;
                Feedback.Sfx("warn", 0.5f);
            }

            if (o.patience <= 0f) FailOrder(i);
        }
    }

    void SpawnOrder()
    {
        SpeciesDefinition species = PreferenceBook.Species[Random.Range(0, PreferenceBook.Species.Length)];

        var order = new Order
        {
            speciesId = species.id,
            maxPatience = species.patienceSeconds,
            patience = species.patienceSeconds
        };

        int count = Random.Range(species.minIngredients, species.maxIngredients + 1);
        for (int i = 0; i < count; i++)
            order.required.Add(PickWeightedFood(species.id));

        order.reward = 0; // більше не використовується, рахує Checkout.BaseCost при видачі
        order.alien = Instantiate(alienPrefab, spawnPoint.position, spawnPoint.rotation);
        order.alien.Init(order, chef, species);

        _queue.Add(order);
        AssignQueueSpots();

        Feedback.Sfx("bell", 0.5f);
        Debug.Log($"[Orders] {species.displayName} хоче {order}");
    }

    // 75% шанс узяти інгредієнт+стан, який цей вид Likes/Loves хоч у якомусь валідному стані;
    // 25% — чистий нейтральний випадковий вибір
    string PickWeightedFood(string speciesId)
    {
        if (Random.value < 0.75f)
        {
            var liked = new List<string>();
            foreach (string food in PreferenceBook.Foods)
                foreach (FoodState state in PreferenceBook.StatesOf(food))
                {
                    PreferenceLevel lvl = PreferenceBook.GetPreference(speciesId, food, state);
                    if (lvl == PreferenceLevel.Likes || lvl == PreferenceLevel.Loves)
                        liked.Add(food);
                }

            if (liked.Count > 0) return liked[Random.Range(0, liked.Count)];
        }

        return ingredientPool[Random.Range(0, ingredientPool.Length)];
    }

    void AssignQueueSpots()
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            _queue[i].alien.MoveTo(queueSpots[i].position);
            _queue[i].alien.SetFront(i == 0);
        }
    }

    public bool TrySubmit(Dish dish)
    {
        if (!_running) return false;

        if (_queue.Count == 0)
        {
            Debug.Log("[Orders] черга порожня, нікому видавати");
            Feedback.Warn("Черга порожня, нікому видавати");
            return false;
        }

        Order first = _queue[0];

        if (!SameIngredients(first.required, dish))
        {
            Debug.Log($"[Orders] {dish} не відповідає замовленню першого клієнта {first}");
            Feedback.Warn("Склад страви не збігається із замовленням");
            first.alien.React("Це не те, що я просив!", Orange, 2f);
            return false;
        }

        float patienceFraction = first.maxPatience > 0f ? first.patience / first.maxPatience : 0f;
        Checkout.Result result = Checkout.Evaluate(first.speciesId, dish, patienceFraction);

        if (result.refused)
        {
            Debug.Log($"[Orders] перший клієнт ({first.speciesId}) відмовився: {result.reason}");
            Feedback.Sfx("angry");
            Feedback.Toast($"Клієнт відмовився: {result.reason}", Red);
            first.alien.React("Фу! Це я їсти не буду!", Red, 2.5f);
            return false;
        }

        int baseCost = Checkout.BaseCost(dish, markupMultiplier);
        int finalReward = Mathf.RoundToInt(baseCost * result.payFactor);

        Served++;
        Credits += finalReward;
        GameSession.AddCredits(finalReward);

        // Реакція за тим, наскільки страва сподобалась
        string line;
        Color col;
        if (result.payFactor >= happyFactor) { line = "Смакота!"; col = Green; Feedback.Sfx("happy"); }
        else if (result.payFactor >= okFactor) { line = "Дякую!"; col = Cyan; }
        else { line = "Таке собі…"; col = Orange; }

        Feedback.Sfx("coin");
        Feedback.FloatText(first.alien.transform.position + Vector3.up * 2f, $"+{finalReward}", Gold, 3f);

        first.alien.Leave(exitPoint.position, true,
            $"<color=#{ColorUtility.ToHtmlStringRGB(col)}><b>{line}</b></color>");
        _queue.RemoveAt(0);
        AssignQueueSpots();

        Debug.Log($"[Orders] видано {dish} для {first}, база {baseCost}, {result.reason} -> +{finalReward}");
        return true;
    }

    // Склад страви збігається з замовленням за назвами, незалежно від стану
    static bool SameIngredients(List<string> required, Dish dish)
    {
        if (dish.parts.Count != required.Count) return false;

        var need = new List<string>(required);
        foreach (var part in dish.parts)
            if (!need.Remove(part.foodName)) return false;

        return need.Count == 0;
    }

    void FailOrder(int index)
    {
        Order o = _queue[index];
        Lost++;
        o.alien.Leave(exitPoint.position, false,
            $"<color=#{ColorUtility.ToHtmlStringRGB(Red)}><b>Йду голодний!</b></color>");
        _queue.RemoveAt(index);
        AssignQueueSpots();

        if (_running)
        {
            Feedback.Sfx("lost");
            Feedback.Toast("Клієнт пішов без їжі", Red);
        }

        Debug.Log($"[Orders] клієнт пішов без їжі: {o}. Втрачено клієнтів: {Lost}");
    }

    void EndShift()
    {
        _running = false;

        for (int i = _queue.Count - 1; i >= 0; i--) FailOrder(i);

        foreach (Station s in FindObjectsByType<Station>(FindObjectsSortMode.None))
            s.ClearContents();

        GameSession.RegisterShiftPlayed();

        Debug.Log($"[Shift] зміну завершено. Обслужено: {Served}, втрачено: {Lost}, зароблено: {Credits}");

        if (HubUI.Instance != null)
            HubUI.Instance.ShowResult(Served, Lost, Credits);
    }
}