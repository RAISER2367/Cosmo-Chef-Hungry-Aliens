using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IngredientConfig
{
    public string id;
    public int baseCapacity = 20;
    public int startAmount = 20;
    public int pricePerUnit = 2;
    public float leadTimeSeconds = 15f;   // час приходу дрона після замовлення
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
        if (GameFlow.Phase != GamePhase.Shift) return;

        CheckDeliveries();

        if (!GameSession.Owns("autobuy")) return;

        _autoBuyTimer -= Time.deltaTime;
        if (_autoBuyTimer > 0f) return;
        _autoBuyTimer = autoBuyCheckInterval;

        foreach (var r in _byId.Values) CheckAutoBuy(r);
    }

    void CheckDeliveries()
    {
        foreach (var r in _byId.Values)
        {
            if (r.pendingArrival < 0f) continue;
            if (Time.time < r.pendingArrival) continue;

            r.current = Mathf.Min(Capacity(r.config.id), r.current + r.pendingAmount);
            Debug.Log($"[Inventory] дрон доставив {r.pendingAmount}x {r.config.id}, склад: {r.current}/{Capacity(r.config.id)}");
            r.pendingArrival = -1f;
            r.pendingAmount = 0;
        }
    }

    void CheckAutoBuy(IngredientRuntime r)
    {
        if (r.pendingArrival >= 0f) return; // вже в дорозі
        int cap = Capacity(r.config.id);
        if (r.current >= cap * 0.5f) return;

        int missing = cap - r.current;
        int cost = missing * r.config.pricePerUnit;

        GameSession.SpendOrDebt(cost);
        r.current = cap;

        Debug.Log($"[Inventory] автозакупівля {r.config.id}: +{missing}, −{cost} кредитів");
    }

    // Ручне замовлення через SupplyTerminal. Повертає false, якщо вже в дорозі.
    public bool TryOrderSupply(string id, out float eta)
    {
        eta = 0f;
        if (!_byId.TryGetValue(id, out var r)) return false;
        if (r.pendingArrival >= 0f) { eta = r.pendingArrival - Time.time; return false; }

        int amount = Mathf.Min(r.config.orderBatch, Capacity(id) - r.current);
        if (amount <= 0) return false; // склад уже повний

        int cost = amount * r.config.pricePerUnit;
        GameSession.SpendOrDebt(cost);

        r.pendingAmount = amount;
        r.pendingArrival = Time.time + r.config.leadTimeSeconds;
        eta = r.config.leadTimeSeconds;

        Debug.Log($"[Inventory] замовлено {amount}x {id} за {cost}, приліт за {r.config.leadTimeSeconds:F0}с");
        return true;
    }

    public bool TryTake(string id)
    {
        if (!_byId.TryGetValue(id, out var r) || r.current <= 0) return false;
        r.current--;
        return true;
    }

    public int Current(string id) => _byId.TryGetValue(id, out var r) ? r.current : 0;

    public int Capacity(string id) =>
        _byId.TryGetValue(id, out var r) ? r.config.baseCapacity + GameSession.FridgeCapacityBonus : 0;

    public int PricePerUnit(string id) => _byId.TryGetValue(id, out var r) ? r.config.pricePerUnit : 0;

    // Для індикатора: >0 секунд, якщо дрон у дорозі, інакше -1
    public float PendingSeconds(string id) =>
        _byId.TryGetValue(id, out var r) && r.pendingArrival >= 0f ? Mathf.Max(0f, r.pendingArrival - Time.time) : -1f;
}