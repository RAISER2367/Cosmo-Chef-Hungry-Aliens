using System.Collections.Generic;
using UnityEngine;

public enum StationType { Fridge, CuttingTable, Stove, AssemblyTable, ServingCounter, TrashBin, StorageTable }

public class Station : MonoBehaviour
{
    public StationType type;

    [Header("Тільки для Fridge")]
    public string fridgeFood = "meat";
    public bool fridgeNeedsCutting = false;

    [Header("Тільки для StorageTable")]
    public int capacity = 4;

    [Header("Тільки для AssemblyTable")]
    public int plateCapacity = 3;

    [Header("Візуал")]
    public FoodVisual foodVisual;      // плита або дошка
    public FoodVisual[] storageSlots;  // сховище
    public FoodVisual[] plateSlots;    // тарілка

    FoodItem _onTop;
    int _cutPresses;
    readonly List<FoodItem> _items = new List<FoodItem>();
    Dish _dish;

    void Start()
    {
        if (type == StationType.AssemblyTable)
        {
            _dish = new Dish();
            if (plateSlots == null || plateSlots.Length < plateCapacity)
                Debug.LogWarning($"[{name}] Plate Slots не заповнені: потрібно {plateCapacity}");
        }

        if (type == StationType.StorageTable && (storageSlots == null || storageSlots.Length < capacity))
            Debug.LogWarning($"[{name}] Storage Slots не заповнені: потрібно {capacity}");
    }

    void Update()
    {
        if (type != StationType.Stove || _onTop == null) return;

        _onTop.cookTime += Time.deltaTime;
        float t = _onTop.cookTime;

        // Таймінги з GDD: 0–4 сек сирий, 5–8 готовий, 9+ згорілий
        FoodState s = t < 5f ? FoodState.Raw : t < 9f ? FoodState.Ready : FoodState.Burnt;
        if (s != _onTop.state)
        {
            _onTop.state = s;
            Log($"{_onTop} at {t:F2}s");
        }
    }

    void LateUpdate()
    {
        if (type == StationType.StorageTable && storageSlots != null)
        {
            for (int i = 0; i < storageSlots.Length; i++)
                storageSlots[i].Apply(i < _items.Count ? _items[i] : null);
            return;
        }

        if (type == StationType.AssemblyTable && plateSlots != null && _dish != null)
        {
            for (int i = 0; i < plateSlots.Length; i++)
                plateSlots[i].Apply(i < _dish.parts.Count ? _dish.parts[i] : null);
            return;
        }

        if (foodVisual != null) foodVisual.Apply(_onTop);
    }

    public void Interact(ChefInteraction chef)
    {
        // Страву можна віддати тільки на видачу або викинути
        if (chef.Hand is Dish && type != StationType.ServingCounter && type != StationType.TrashBin)
        {
            Log("страву можна віддати лише на видачу або викинути");
            return;
        }

        switch (type)
        {
            case StationType.Fridge: HandleFridge(chef); break;
            case StationType.CuttingTable: HandleCutting(chef); break;
            case StationType.Stove: HandleStove(chef); break;
            case StationType.AssemblyTable: HandleAssembly(chef); break;
            case StationType.ServingCounter: HandleServing(chef); break;
            case StationType.TrashBin: HandleTrash(chef); break;
            case StationType.StorageTable: HandleStorage(chef); break;
        }
    }

    void HandleFridge(ChefInteraction chef)
    {
        if (chef.Hand != null) { Log("руки зайняті"); return; }
        chef.Hand = new FoodItem(fridgeFood, fridgeNeedsCutting);
        Log($"взяв {chef.Hand}");
    }

    void HandleCutting(ChefInteraction chef)
    {
        if (_onTop == null)
        {
            if (chef.Hand != null && chef.Hand.needsCutting)
            {
                _onTop = chef.Hand;
                chef.Hand = null;
                _cutPresses = 0;
                Log($"поклав {_onTop} на дошку");
            }
            return;
        }

        if (chef.Hand != null) return;

        if (_onTop.needsCutting)
        {
            _cutPresses++;
            Log($"різання {_cutPresses}/3");
            if (_cutPresses >= 3)
            {
                _onTop.needsCutting = false;
                _onTop.state = FoodState.Cut;
                Log($"{_onTop} нарізано");
            }
        }
        else
        {
            chef.Hand = _onTop;
            _onTop = null;
            Log($"взяв {chef.Hand} з дошки");
        }
    }

    void HandleStove(ChefInteraction chef)
    {
        if (_onTop == null && chef.Hand != null)
        {
            _onTop = chef.Hand;
            chef.Hand = null;
            Log($"поклав {_onTop} на плиту");
        }
        else if (_onTop != null && chef.Hand == null)
        {
            chef.Hand = _onTop;
            _onTop = null;
            Log($"забрав {chef.Hand} з плити");
        }
        else if (_onTop != null && chef.Hand != null)
        {
            FoodItem tmp = _onTop;
            _onTop = chef.Hand;
            chef.Hand = tmp;
            Log($"обмін на плиті: {_onTop}, у руках {chef.Hand}");
        }
        else
        {
            Log("плита порожня, у руках нічого");
        }
    }

    void HandleStorage(ChefInteraction chef)
    {
        if (chef.Hand == null)
        {
            if (_items.Count == 0) { Log("сховище порожнє"); return; }

            // Якщо цілимо в конкретний слот, беремо його; інакше верхній продукт
            int idx = (chef.TargetSlot >= 0 && chef.TargetSlot < _items.Count)
                ? chef.TargetSlot
                : _items.Count - 1;

            FoodItem item = _items[idx];
            _items.RemoveAt(idx);
            chef.Hand = item;
            Log($"взяв {item} зі сховища (слот {idx}), лишилось {_items.Count}/{capacity}");
            return;
        }

        if (_items.Count >= capacity) { Log("сховище повне"); return; }

        _items.Add(chef.Hand);
        Log($"поклав {chef.Hand} у сховище, {_items.Count}/{capacity}");
        chef.Hand = null;
    }

    void HandleAssembly(ChefInteraction chef)
    {
        if (chef.Hand == null)
        {
            // Компоненти назад не беремо. Готову страву беремо цілком, після чого з'являється нова тарілка
            _dish.Refresh();
            if (_dish.state != FoodState.Ready)
            {
                Log($"страва ще не готова ({_dish.parts.Count}/{plateCapacity}), компоненти назад не беремо");
                return;
            }

            chef.Hand = _dish;
            Log($"взяв готову страву {_dish}");
            _dish = new Dish();
            return;
        }

        // Сире і підгоріле в страву не кладемо (за GDD)
        if (chef.Hand.state == FoodState.Raw || chef.Hand.state == FoodState.Burnt)
        {
            Log($"{chef.Hand} не можна покласти в страву");
            return;
        }

        if (_dish.parts.Count >= plateCapacity) { Log("тарілка повна"); return; }

        _dish.parts.Add(chef.Hand);
        _dish.Refresh();
        Log($"додав {chef.Hand} у страву, {_dish.parts.Count}/{plateCapacity}");
        chef.Hand = null;
    }

    void HandleServing(ChefInteraction chef)
    {
        if (chef.Hand == null) { Log("руки порожні"); return; }

        bool ready = chef.Hand is Dish d
            ? d.state == FoodState.Ready
            : chef.Hand.state == FoodState.Ready;

        if (!ready)
        {
            Log($"не можна видати {chef.Hand}");
            return;
        }

        Log($"видав {chef.Hand}");
        chef.Hand = null;
    }

    void HandleTrash(ChefInteraction chef)
    {
        if (chef.Hand == null) { Log("руки порожні"); return; }
        Log($"викинув {chef.Hand}");
        chef.Hand = null;
    }

    void Log(string msg) => Debug.Log($"[{name}] {msg}");
}