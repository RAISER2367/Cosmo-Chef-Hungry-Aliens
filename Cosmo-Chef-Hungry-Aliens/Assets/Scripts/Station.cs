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

    [Header("Візуал дошки")]
    public FoodVisual foodVisual;

    [Header("Візуал сховища")]
    public FoodVisual[] storageSlots;

    [Header("Візуал тарілки")]
    public FoodVisual[] plateSlots;

    [Header("Плита: комфорки")]
    public GameObject[] burnerPads;     // блюдця, вимикаються, доки комфорка не куплена
    public FoodVisual[] burnerVisuals;  // FoodOnBurner на кожному блюдці

    FoodItem _onTop;                    // дошка (CuttingTable)
    int _cutPresses;
    readonly List<FoodItem> _items = new List<FoodItem>();
    readonly FoodItem[] _burners = new FoodItem[4];
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
        if (type == StationType.Stove) TickBurners();
    }

    void TickBurners()
    {
        int n = Mathf.Min(GameSession.BurnerCount, _burners.Length);
        for (int i = 0; i < n; i++)
        {
            FoodItem f = _burners[i];

            // Нарізане не готується: таймер не має перезаписувати стан Cut
            if (f == null || f.state == FoodState.Cut) continue;

            f.cookTime += Time.deltaTime;
            float t = f.cookTime;

            // Таймінги з GDD: 0–4 сирий, 5–8 готовий, 9+ згорілий
            FoodState s = t < 5f ? FoodState.Raw : t < 9f ? FoodState.Ready : FoodState.Burnt;
            if (s != f.state)
            {
                f.state = s;
                Log($"комфорка {i}: {f} at {t:F2}s");
            }
        }
    }

    void LateUpdate()
    {
        switch (type)
        {
            case StationType.Stove: ShowBurners(); return;
            case StationType.StorageTable: ShowStorage(); return;
            case StationType.AssemblyTable: ShowPlate(); return;
        }

        if (foodVisual != null) foodVisual.Apply(_onTop);
    }

    void ShowBurners()
    {
        int n = GameSession.BurnerCount;

        for (int i = 0; i < _burners.Length; i++)
        {
            bool unlocked = i < n;

            if (burnerPads != null && i < burnerPads.Length && burnerPads[i] != null)
                burnerPads[i].SetActive(unlocked);

            if (burnerVisuals != null && i < burnerVisuals.Length && burnerVisuals[i] != null)
                burnerVisuals[i].Apply(unlocked ? _burners[i] : null);
        }
    }

    void ShowStorage()
    {
        if (storageSlots == null) return;

        for (int i = 0; i < storageSlots.Length; i++)
            storageSlots[i].Apply(i < _items.Count ? _items[i] : null);
    }

    void ShowPlate()
    {
        if (plateSlots == null || _dish == null) return;

        for (int i = 0; i < plateSlots.Length; i++)
            plateSlots[i].Apply(i < _dish.parts.Count ? _dish.parts[i] : null);
    }

    public void Interact(ChefInteraction chef)
    {
        // Страву можна лише віддати на видачу або викинути
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

    // ---------- Джерело інгредієнтів ----------

    void HandleFridge(ChefInteraction chef)
    {
        if (chef.Hand != null) { Log("руки зайняті"); return; }

        chef.Hand = new FoodItem(fridgeFood, fridgeNeedsCutting);
        Log($"взяв {chef.Hand}");
    }

    // ---------- Дошка ----------
    // Правила:
    // - порожня дошка: кладемо продукт, що потребує нарізання;
    // - на дошці ненарізане, руки порожні: нарізаємо тричі;
    // - на дошці нарізане, руки порожні: беремо;
    // - на дошці щось є, у руках продукт для нарізання: обмін.

    void HandleCutting(ChefInteraction chef)
    {
        // Порожня дошка
        if (_onTop == null)
        {
            if (chef.Hand == null) { Log("дошка порожня, у руках нічого"); return; }
            if (!chef.Hand.needsCutting) { Log($"{chef.Hand} не потребує нарізання"); return; }

            PlaceOnBoard(chef.Hand);
            chef.Hand = null;
            Log($"поклав {_onTop} на дошку");
            return;
        }

        // Дошка зайнята, руки порожні
        if (chef.Hand == null)
        {
            if (_onTop.needsCutting)
            {
                PressCut();
                return;
            }

            chef.Hand = _onTop;
            _onTop = null;
            _cutPresses = 0;
            ShowCut(0f, false);
            Log($"взяв {chef.Hand} з дошки");
            return;
        }

        // Дошка зайнята, руки теж: обмін, якщо в руках є що нарізати
        if (chef.Hand.needsCutting)
        {
            FoodItem boardItem = _onTop;
            PlaceOnBoard(chef.Hand);
            chef.Hand = boardItem;
            Log($"обмін на дошці: на дошці {_onTop}, у руках {chef.Hand}");
            return;
        }

        Log("щоб забрати з дошки, руки мають бути порожні або тримати продукт для нарізання");
    }

    void PlaceOnBoard(FoodItem item)
    {
        _onTop = item;
        _cutPresses = 0;
    }

    void PressCut()
    {
        _cutPresses++;
        ShowCut(_cutPresses / 3f, true);
        Log($"різання {_cutPresses}/3");

        if (_cutPresses >= 3)
        {
            _onTop.needsCutting = false;
            _onTop.state = FoodState.Cut;
            Log($"{_onTop} нарізано");
        }
    }

    void ShowCut(float progress, bool punch)
    {
        if (foodVisual != null) foodVisual.SetCutProgress(progress, punch);
    }

    // ---------- Плита ----------
    // Наведення на блюдце працює напряму. Якщо наведено на корпус або на
    // заблоковану комфорку, береться найближча відкрита:
    // - руки порожні: беремо з найближчої зайнятої;
    // - руки з їжею: кладемо на найближчу вільну, а якщо всі зайняті,
    //   міняємо з найближчою зайнятою.

    void HandleStove(ChefInteraction chef)
    {
        int n = Mathf.Min(GameSession.BurnerCount, _burners.Length);

        int slot = chef.TargetSlot;
        if (slot < 0 || slot >= n) slot = -1;

        // Руки порожні: беремо
        if (chef.Hand == null)
        {
            int idx = slot >= 0 ? slot : NearestBurner(chef.transform.position, true);
            if (idx < 0) { Log("плита порожня"); return; }
            if (_burners[idx] == null) { Log($"комфорка {idx} порожня"); return; }

            chef.Hand = _burners[idx];
            _burners[idx] = null;
            Log($"взяв {chef.Hand} з комфорки {idx}");
            return;
        }

        // Руки з їжею
        int target = slot;
        if (target < 0)
        {
            target = NearestBurner(chef.transform.position, false);      // спершу вільна
            if (target < 0)
                target = NearestBurner(chef.transform.position, true);   // усі зайняті: обмін
        }

        if (target < 0) { Log("немає відкритих комфорок"); return; }

        if (_burners[target] == null)
        {
            _burners[target] = chef.Hand;
            chef.Hand = null;
            Log($"поклав {_burners[target]} на комфорку {target}");
            return;
        }

        // Обмін: їжа з рук іде на комфорку, а з комфорки в руки
        FoodItem placed = chef.Hand;
        chef.Hand = _burners[target];
        _burners[target] = placed;
        Log($"обмін на комфорці {target}: на плиті {placed}, у руках {chef.Hand}");
    }

    // occupied = true: шукаємо зайняту; false: вільну
    int NearestBurner(Vector3 from, bool occupied)
    {
        int n = Mathf.Min(GameSession.BurnerCount, _burners.Length);
        int best = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            bool isOccupied = _burners[i] != null;
            if (isOccupied != occupied) continue;

            float d = (BurnerPosition(i) - from).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    Vector3 BurnerPosition(int i)
    {
        if (burnerPads != null && i < burnerPads.Length && burnerPads[i] != null)
            return burnerPads[i].transform.position;

        return transform.position;
    }

    // ---------- Сховище ----------

    void HandleStorage(ChefInteraction chef)
    {
        if (chef.Hand == null)
        {
            if (_items.Count == 0) { Log("сховище порожнє"); return; }

            // Наведено на слот: беремо його. Інакше верхній продукт.
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

    // ---------- Тарілка ----------

    void HandleAssembly(ChefInteraction chef)
    {
        if (chef.Hand == null)
        {
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

    // ---------- Видача і смітник ----------

    void HandleServing(ChefInteraction chef)
    {
        if (chef.Hand == null) { Log("руки порожні"); return; }

        if (!(chef.Hand is Dish dish))
        {
            Log($"{chef.Hand} без тарілки не можна видати");
            return;
        }

        if (OrderManager.Instance == null)
        {
            Log("немає OrderManager у сцені");
            return;
        }

        if (OrderManager.Instance.TrySubmit(dish))
            chef.Hand = null;
    }

    void HandleTrash(ChefInteraction chef)
    {
        if (chef.Hand == null) { Log("руки порожні"); return; }

        Log($"викинув {chef.Hand}");
        chef.Hand = null;
    }

    // ---------- Очищення в кінці зміни ----------

    public void ClearContents()
    {
        _onTop = null;
        _cutPresses = 0;
        _items.Clear();
        System.Array.Clear(_burners, 0, _burners.Length);

        if (type == StationType.AssemblyTable) _dish = new Dish();
    }

    void Log(string msg) => Debug.Log($"[{name}] {msg}");
}