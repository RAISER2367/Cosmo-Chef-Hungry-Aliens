using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IngredientConfig
{
    public string id;
    public int baseCapacity = 20;
    public int startAmount = 20;
    public int pricePerUnit = 2;
    public float leadTimeSeconds = 15f;   // базовий час приходу дрона після замовлення
    public int orderBatch = 8;            // скільки одиниць привозить одне замовлення
}

class IngredientRuntime
{
    public IngredientConfig config;
    public int current;
    public float pendingArrival = -1f;    // Time.time приходу; -1 = нема активного замовлення
    public int pendingAmount;
}

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [SerializeField]
    IngredientConfig[] configs =
    {
        new IngredientConfig { id = "meat",     baseCapacity = 20, startAmount = 20, pricePerUnit = 3, leadTimeSeconds = 18f, orderBatch = 8 },
        new IngredientConfig { id = "cucumber", baseCapacity = 20, startAmount = 20, pricePerUnit = 1, leadTimeSeconds = 10f, orderBatch = 8 },
    };

    [SerializeField] float autoBuyCheckInterval = 2f;
    [SerializeField] int lowStockWarning = 3;
    float _autoBuyTimer;

    readonly Dictionary<string, IngredientRuntime> _byId = new Dictionary<string, IngredientRuntime>();

    void Awake()
    {
        Instance = this;
        foreach (var c in configs)
            _byId[c.id] = new IngredientRuntime { config = c, current = c.startAmount };
    }

    void Update()
    {
        // Дрон, що вже летить, прилітає і в хабі, але автозакупівля працює лише на зміні
        CheckDeliveries();

        if (GameFlow.Phase != GamePhase.Shift) return;

        float threshold = GameSession.AutoBuyThreshold;
        if (threshold <= 0f) return;

        _autoBuyTimer -= Time.deltaTime;
        if (_autoBuyTimer > 0f) return;
        _autoBuyTimer = autoBuyCheckInterval;

        foreach (var r in _byId.Values) CheckAutoBuy(r, threshold);
    }

    void CheckDeliveries()
    {
        foreach (var r in _byId.Values)
        {
            if (r.pendingArrival < 0f) continue;
            if (Time.time < r.pendingArrival) continue;

            int before = r.current;
            r.current = Mathf.Min(Capacity(r.config.id), r.current + r.pendingAmount);
            int delivered = r.current - before;

            Debug.Log($"[Inventory] дрон доставив {delivered}x {r.config.id}, склад: {r.current}/{Capacity(r.config.id)}");
            Feedback.Good($"Дрон доставив {delivered}× {Labels.Food(r.config.id)}");
            Feedback.Sfx("deliver");

            r.pendingArrival = -1f;
            r.pendingAmount = 0;
        }
    }

    void CheckAutoBuy(IngredientRuntime r, float threshold)
    {
        if (r.pendingArrival >= 0f) return; // вже в дорозі

        int cap = Capacity(r.config.id);
        if (r.current >= cap * threshold) return;

        float eta;
        if (BeginDelivery(r, out eta))
            Feedback.Toast($"Автологістика: {Labels.Food(r.config.id)}, дрон прилетить за {eta:F0}с", new Color(0.6f, 0.85f, 1f));
    }

    // Єдиний шлях замовлення (ручного й автоматичного): гроші списуються одразу, товар приходить із затримкою
    bool BeginDelivery(IngredientRuntime r, out float eta)
    {
        eta = 0f;

        int amount = Mathf.Min(r.config.orderBatch, Capacity(r.config.id) - r.current);
        if (amount <= 0) return false; // склад уже повний

        int cost = amount * r.config.pricePerUnit;
        GameSession.SpendOrDebt(cost);

        float lead = GameSession.TurboDroneLeadTime(r.config.leadTimeSeconds);
        r.pendingAmount = amount;
        r.pendingArrival = Time.time + lead;
        eta = lead;

        Debug.Log($"[Inventory] замовлено {amount}x {r.config.id} за {cost}, приліт за {lead:F0}с");
        return true;
    }

    // Ручне замовлення через SupplyTerminal. Повертає false, якщо вже в дорозі.
    public bool TryOrderSupply(string id, out float eta)
    {
        eta = 0f;
        if (!_byId.TryGetValue(id, out var r)) return false;
        if (r.pendingArrival >= 0f) { eta = r.pendingArrival - Time.time; return false; }

        return BeginDelivery(r, out eta);
    }

    public bool TryTake(string id)
    {
        if (!_byId.TryGetValue(id, out var r) || r.current <= 0) return false;

        r.current--;

        if (r.current == 0)
            Feedback.Warn($"{Labels.Food(id)} закінчилось на складі!");
        else if (r.current == lowStockWarning)
            Feedback.Toast($"Мало: {Labels.Food(id)}, лишилось {r.current}", new Color(1f, 0.8f, 0.4f));

        return true;
    }

    public int Current(string id) => _byId.TryGetValue(id, out var r) ? r.current : 0;

    public int Capacity(string id) =>
        _byId.TryGetValue(id, out var r) ? r.config.baseCapacity + GameSession.FridgeCapacityBonus : 0;

    public int PricePerUnit(string id) => _byId.TryGetValue(id, out var r) ? r.config.pricePerUnit : 0;

    // Вартість наступного замовлення (0, якщо склад повний)
    public int NextOrderCost(string id)
    {
        if (!_byId.TryGetValue(id, out var r)) return 0;
        int amount = Mathf.Min(r.config.orderBatch, Capacity(id) - r.current);
        return Mathf.Max(0, amount) * r.config.pricePerUnit;
    }

    // Для індикатора: >0 секунд, якщо дрон у дорозі, інакше -1
    public float PendingSeconds(string id) =>
        _byId.TryGetValue(id, out var r) && r.pendingArrival >= 0f ? Mathf.Max(0f, r.pendingArrival - Time.time) : -1f;
}