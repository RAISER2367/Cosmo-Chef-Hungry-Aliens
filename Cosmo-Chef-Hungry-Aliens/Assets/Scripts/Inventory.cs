using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IngredientStock
{
    public string id;
    public int capacity = 20;
    public int current = 20;
    public int pricePerUnit = 2;
}

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [SerializeField]
    IngredientStock[] stocks =
    {
        new IngredientStock { id = "meat", capacity = 20, current = 20, pricePerUnit = 2 },
        new IngredientStock { id = "cucumber", capacity = 20, current = 20, pricePerUnit = 1 },
    };

    [SerializeField] float checkInterval = 2f;
    float _timer;

    Dictionary<string, IngredientStock> _byId;

    void Awake()
    {
        Instance = this;
        _byId = new Dictionary<string, IngredientStock>();
        foreach (var s in stocks) _byId[s.id] = s;
    }

    void Update()
    {
        if (GameFlow.Phase != GamePhase.Shift) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = checkInterval;

        foreach (var s in stocks) CheckAutoBuy(s);
    }

    void CheckAutoBuy(IngredientStock s)
    {
        if (s.current >= s.capacity * 0.5f) return;

        int missing = s.capacity - s.current;
        int cost = missing * s.pricePerUnit;

        s.current = s.capacity;
        GameSession.SpendOrDebt(cost);

        Debug.Log($"[Inventory] автозакупівля {s.id}: +{missing}, −{cost} кредитів");
    }

    public bool TryTake(string id)
    {
        if (!_byId.TryGetValue(id, out var s) || s.current <= 0) return false;
        s.current--;
        return true;
    }

    public int Current(string id) => _byId.TryGetValue(id, out var s) ? s.current : 0;
    public int Capacity(string id) => _byId.TryGetValue(id, out var s) ? s.capacity : 0;
}