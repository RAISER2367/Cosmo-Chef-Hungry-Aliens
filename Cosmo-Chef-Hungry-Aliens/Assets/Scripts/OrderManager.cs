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

    public override string ToString() => $"[{speciesId}: {string.Join(", ", required)}]";
}

public class OrderManager : MonoBehaviour
{
    [Header("Економіка")]
    [SerializeField] float markupMultiplier = 3.5f; // нагорода = собівартість інгредієнтів * це число

    [Header("Борг")]
    [SerializeField] float dailyInterestRate = 0.05f; // 5% на день для Standard/Marathon
    int _interestDaysApplied;
    public static OrderManager Instance { get; private set; }


    [Header("Клієнти")]
    [SerializeField] Alien alienPrefab;
    [SerializeField] Transform spawnPoint;          // де з'являються, поза трейлером
    [SerializeField] Transform exitPoint;           // куди ідуть після обслуговування
    [SerializeField] Transform[] queueSpots;        // 0 = найближче до вікна
    [SerializeField] float spawnInterval = 12f;
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

    void Awake()
    {
        Instance = this;
    }

    public void StartShift()
    {
        float duration = ShiftSettings.Duration;

        _shiftLeft = duration;
        _spawnTimer = 0f;
        _running = true;
        _interestDaysApplied = 0;

        GameSession.BeginShiftAccounting();
        BuildTimerMilestones(duration);
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

        while (_interestDaysApplied < dayIndex && _interestDaysApplied < ShiftSettings.Days)
        {
            _interestDaysApplied++;
            if (ShiftSettings.Days > 1) GameSession.ApplyDailyInterest(dailyInterestRate);
        }

        // Дебаг-таймер: while, щоб не пропустити кілька позначок за один кадр
        while (_timerMilestones.Count > 0 && _shiftLeft <= _timerMilestones.Peek())
        {
            float mark = _timerMilestones.Dequeue();
            if (mark >= 60f)
                Debug.Log($"[Таймер] Залишилося: {mark / 60f} хв");
            else
                Debug.Log($"[Таймер] Залишилося: {mark} сек!");
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
            _spawnTimer = spawnInterval;
        }

        // Йдемо з кінця, щоб видалення не зсувало індекси
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            Order o = _queue[i];
            o.patience -= Time.deltaTime;
            o.alien.SetPatience(o.patience / o.maxPatience);

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
        int costSum = 0;

        for (int i = 0; i < count; i++)
        {
            string food = ingredientPool[Random.Range(0, ingredientPool.Length)];
            order.required.Add(food);
            costSum += Inventory.Instance != null ? Inventory.Instance.PricePerUnit(food) : 2;
        }

        order.reward = Mathf.RoundToInt(costSum * markupMultiplier * species.rewardMultiplier);
        order.alien = Instantiate(alienPrefab, spawnPoint.position, spawnPoint.rotation);
        order.alien.Init(order, chef, species);

        _queue.Add(order);
        AssignQueueSpots();
        Debug.Log($"[Orders] {species.displayName} хоче {order}, собівартість {costSum}, нагорода {order.reward}");
    }

    void AssignQueueSpots()
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            _queue[i].alien.MoveTo(queueSpots[i].position);
            _queue[i].alien.SetFront(i == 0);
        }
    }

    //public bool TrySubmit(Dish dish)
    //{
    //    if (!_running) return false;

    //    for (int i = 0; i < _queue.Count; i++)
    //    {
    //        Order o = _queue[i];
    //        if (!SameIngredients(o.required, dish)) continue;

    //        var (ok, multiplier, reaction) = EvaluatePreferences(o.speciesId, dish);
    //        if (!ok)
    //        {
    //            Debug.Log($"[Orders] {o.alien.name} відмовився: {reaction}");
    //            continue; // пробуємо наступне замовлення в черзі
    //        }

    //        int finalReward = Mathf.RoundToInt(o.reward * multiplier);

    //        Served++;
    //        Credits += finalReward;
    //        GameSession.AddCredits(finalReward);
    //        o.alien.Leave(exitPoint.position, true);
    //        _queue.RemoveAt(i);
    //        AssignQueueSpots();

    //        Debug.Log($"[Orders] видано {dish} для {o}, {reaction}, +{finalReward}");
    //        return true;
    //    }

    //    Debug.Log($"[Orders] {dish} не відповідає жодному замовленню в черзі");
    //    return false;
    //}

    public bool TrySubmit(Dish dish)
    {
        if (!_running) return false;

        if (_queue.Count == 0)
        {
            Debug.Log("[Orders] черга порожня, нікому видавати");
            return false;
        }

        Order first = _queue[0];

        if (!SameIngredients(first.required, dish))
        {
            Debug.Log($"[Orders] {dish} не відповідає замовленню першого клієнта {first}");
            return false;
        }

        var (ok, multiplier, reaction) = EvaluatePreferences(first.speciesId, dish);
        if (!ok)
        {
            Debug.Log($"[Orders] перший клієнт ({first.speciesId}) відмовився: {reaction}");
            return false;
        }

        int finalReward = Mathf.RoundToInt(first.reward * multiplier);

        Served++;
        Credits += finalReward;
        GameSession.AddCredits(finalReward);
        first.alien.Leave(exitPoint.position, true);
        _queue.RemoveAt(0);
        AssignQueueSpots();

        Debug.Log($"[Orders] видано {dish} для {first}, {reaction}, +{finalReward}");
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

    // Повертає: чи прийнято, множник нагороди, текст реакції для логів/UI
    static (bool ok, float multiplier, string reaction) EvaluatePreferences(string speciesId, Dish dish)
    {
        float sum = 0f;
        foreach (var part in dish.parts)
        {
            PreferenceLevel level = PreferenceBook.GetPreference(speciesId, part.foodName, part.state);

            if (level == PreferenceLevel.Hates)
                return (false, 0f, $"ненавидить {part.foodName} ({part.state})");

            sum += LevelToMultiplier(level);
        }

        float avg = dish.parts.Count > 0 ? sum / dish.parts.Count : 1f;
        return (true, avg, $"середня реакція x{avg:F2}");
    }

    static float LevelToMultiplier(PreferenceLevel level) => level switch
    {
        PreferenceLevel.Dislikes => 0.6f,
        PreferenceLevel.Neutral => 1.0f,
        PreferenceLevel.Likes => 1.4f,
        PreferenceLevel.Loves => 2.0f,
        _ => 1.0f
    };

    static bool Matches(List<string> required, Dish dish)
    {
        if (dish.state != FoodState.Ready) return false;
        if (dish.parts.Count != required.Count) return false;

        var need = new List<string>(required);
        foreach (var part in dish.parts)
        {
            if (!need.Remove(part.foodName)) return false;
        }
        return need.Count == 0;
    }

    void FailOrder(int index)
    {
        Order o = _queue[index];
        Lost++;
        o.alien.Leave(exitPoint.position, false);
        _queue.RemoveAt(index);
        AssignQueueSpots();
        Debug.Log($"[Orders] клієнт пішов без їжі: {o}. Втрачено клієнтів: {Lost}");
    }

    void EndShift()
    {
        _running = false;

        for (int i = _queue.Count - 1; i >= 0; i--) FailOrder(i);

        foreach (Station s in FindObjectsByType<Station>())
            s.ClearContents();

        Debug.Log($"[Shift] зміну завершено. Обслужено: {Served}, втрачено: {Lost}, заробіто: {Credits}");

        if (HubUI.Instance != null)
            HubUI.Instance.ShowResult(Served, Lost, Credits);
    }
}