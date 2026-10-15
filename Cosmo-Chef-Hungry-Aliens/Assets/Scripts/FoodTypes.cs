using System.Collections.Generic;
using UnityEngine;

public enum FoodState { Raw, Cut, Ready, Burnt }

[System.Serializable]
public class FoodItem
{
    static int _nextId = 1;

    public readonly int id;
    public string foodName;
    public FoodState state = FoodState.Raw;
    public bool needsCutting;
    public float cookTime;

    public FoodItem(string name, bool cut = false)
    {
        id = _nextId++;
        foodName = name;
        needsCutting = cut;
    }

    public override string ToString() => $"{foodName}#{id} ({state})";
}

// Тарілка зі стравою: компоненти додаються, але назад не забираються
public class Dish : FoodItem
{
    public readonly List<FoodItem> parts = new List<FoodItem>();

    public Dish() : base("plate") { }

    // Страва готова, якщо є хоча б один компонент і всі вони готові або нарізані
    public void Refresh()
    {
        bool allDone = parts.Count > 0 &&
                       parts.TrueForAll(p => p.state == FoodState.Ready || p.state == FoodState.Cut);
        state = allDone ? FoodState.Ready : FoodState.Raw;
    }

    public override string ToString() => $"plate#{id} [{string.Join(", ", parts)}]";
}