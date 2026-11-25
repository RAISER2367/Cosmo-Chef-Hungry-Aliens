using System.Collections.Generic;
using UnityEngine;

public enum StationType { Fridge, CuttingTable, Stove, AssemblyTable, ServingCounter, TrashBin, StorageTable, SupplyTerminal }

public class Station : MonoBehaviour
{
    public StationType type;

    [Header("Тільки для Fridge")]
    public string fridgeFood = "meat";

    public bool fridgeNeedsCutting = false;
    [Header("Тільки для SupplyTerminal")]
    public string supplyIngredient = "meat";

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

    const float ReadyAt = 5f;           // GDD: 0–4 сирий, 5–8 готовий, 9+ згорілий (9 → 12 з індукційним контуром)
    const float WarnBeforeBurn = 1.5f;

    FoodItem _onTop;                    // дошка (CuttingTable)
    int _cutPresses;
    readonly List<FoodItem> _items = new List<FoodItem>();
    readonly FoodItem[] _burners = new FoodItem[4];
    readonly int[] _warnedFoodId = new int[4];
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
        float burnAt = GameSession.BurnTime;

        for (int i = 0; i < n; i++)
        {
            FoodItem f = _burners[i];

            // Нарізане не готується: таймер не має перезаписувати стан Cut
            if (f == null || f.state == FoodState.Cut) continue;

            f.cookTime += Time.deltaTime;
            float t = f.cookTime;

            FoodState s = t < ReadyAt ? FoodState.Raw : t < burnAt ? FoodState.Ready : FoodState.Burnt;
            Vector3 above = BurnerPosition(i) + Vector3.up * 0.4f;

            if (s != f.state)
            {
                f.state = s;
                Log($"комфорка {i}: {f} at {t:F2}s");

                if (s == FoodState.Ready)
                {
                    Feedback.Sfx("ready");
                    Feedback.FloatText(above, "ГОТОВО", new Color(0.5f, 1f, 0.55f));
                }
                else if (s == FoodState.Burnt)
                {
                    Feedback.Sfx("burnt");
                    Feedback.FloatText(above, "ЗГОРІЛО!", new Color(1f, 0.4f, 0.35f));
                    Feedback.Toast("Згоріло! Викинь у смітник", new Color(1f, 0.5f, 0.4f));
                }
            }
            else if (s == FoodState.Ready && t >= burnAt - WarnBeforeBurn && _warnedFoodId[i] != f.id)
            {
                // Попередження: страва зараз згорить
                _warnedFoodId[i] = f.id;
                Feedback.Sfx("warn", 0.7f);
                Feedback.FloatText(above, "!", new Color(1f, 0.75f, 0.3f));
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

    // ---------- Підказка під прицілом ----------
    // Дзеркалить логіку Interact: що станеться, якщо натиснути E просто зараз.
    // actionable = false: дія неможлива (підказка показується червоною).

    public string GetPrompt(ChefInteraction chef, out bool actionable)
    {
        FoodItem hand = chef.Hand;
        (string text, bool ok) r;

        if (hand is Dish && type != StationType.ServingCounter && type != StationType.TrashBin)
        {
            actionable = false;
            return "Страву — лише на видачу або в смітник";
        }

        switch (type)
        {
            case StationType.Fridge: r = FridgePrompt(hand); break;
            case StationType.CuttingTable: r = CuttingPrompt(hand); break;
            case StationType.Stove: r = StovePrompt(chef, hand); break;
            case StationType.AssemblyTable: r = AssemblyPrompt(hand); break;
            case StationType.ServingCounter: r = ServingPrompt(hand); break;
            case StationType.TrashBin: r = TrashPrompt(hand); break;
            case StationType.StorageTable: r = StoragePrompt(chef, hand); break;
            case StationType.SupplyTerminal: r = SupplyPrompt(); break;
            default: r = ("", false); break;
        }

        actionable = r.ok;
        return r.text;
    }

    (string, bool) FridgePrompt(FoodItem hand)
    {
        string label = Labels.Food(fridgeFood);
        if (hand != null) return ("Руки зайняті", false);
        if (Inventory.Instance == null) return ($"Взяти {label}", true);

        int cur = Inventory.Instance.Current(fridgeFood);
        int cap = Inventory.Instance.Capacity(fridgeFood);
        if (cur <= 0) return ($"{label}: склад порожній", false);
        return ($"Взяти {label}  ({cur}/{cap})", true);
    }

    (string, bool) CuttingPrompt(FoodItem hand)
    {
        if (_onTop == null)
        {
            if (hand == null) return ("Дошка порожня", false);
            if (!hand.needsCutting) return ($"{Labels.Item(hand)} не ріжеться", false);
            return ("Покласти на дошку", true);
        }

        if (hand == null)
        {
            if (_onTop.needsCutting) return ($"Різати ({_cutPresses}/{GameSession.CutPresses})", true);
            return ($"Забрати: {Labels.Item(_onTop)}", true);
        }

        if (hand.needsCutting) return ("Обміняти на дошці", true);
        return ("Руки зайняті", false);
    }

    (string, bool) StovePrompt(ChefInteraction chef, FoodItem hand)
    {
        if (hand == null)
        {
            int idx = StovePickIndex(chef);
            if (idx < 0 || _burners[idx] == null) return ("Плита порожня", false);
            return ($"Забрати: {Labels.Item(_burners[idx])}", true);
        }

        int target = StoveTargetIndex(chef);
        if (target < 0) return ("Немає відкритих комфорок", false);
        return _burners[target] == null ? ("Покласти на комфорку", true) : ("Обміняти на комфорці", true);
    }

    (string, bool) AssemblyPrompt(FoodItem hand)
    {
        if (_dish == null) return ("", false);

        if (hand == null)
        {
            if (_dish.parts.Count == 0) return ("Тарілка порожня", false);
            return ($"Взяти страву ({_dish.parts.Count}/{plateCapacity})", true);
        }

        if (_dish.parts.Count >= plateCapacity) return ("Тарілка повна", false);
        return ($"Додати до страви ({_dish.parts.Count}/{plateCapacity})", true);
    }

    (string, bool) ServingPrompt(FoodItem hand)
    {
        if (hand == null) return ("Руки порожні", false);
        if (hand is Dish) return ("Віддати страву клієнту", true);
        return ("Потрібна тарілка зі стравою", false);
    }

    (string, bool) TrashPrompt(FoodItem hand)
    {
        if (hand == null) return ("Руки порожні", false);
        return ($"Викинути: {Labels.Item(hand)}", true);
    }

    (string, bool) StoragePrompt(ChefInteraction chef, FoodItem hand)
    {
        if (hand == null)
        {
            if (_items.Count == 0) return ("Сховище порожнє", false);
            return ($"Взяти зі сховища ({_items.Count}/{capacity})", true);
        }

        if (_items.Count >= capacity) return ("Сховище повне", false);
        return ($"Покласти у сховище ({_items.Count}/{capacity})", true);
    }

    (string, bool) SupplyPrompt()
    {
        if (Inventory.Instance == null) return ("Термінал не підключено", false);

        string label = Labels.Food(supplyIngredient);
        float pending = Inventory.Instance.PendingSeconds(supplyIngredient);
        if (pending >= 0f) return ($"Дрон із {label} у дорозі: {pending:F0}с", false);

        int cost = Inventory.Instance.NextOrderCost(supplyIngredient);
        if (cost <= 0) return ($"{label}: склад повний", false);
        return ($"Замовити {label}  (−{cost})", true);
    }

    // ---------- Взаємодія ----------

    public void Interact(ChefInteraction chef)
    {
        // Страву можна лише віддати на видачу або викинути
        if (chef.Hand is Dish && type != StationType.ServingCounter && type != StationType.TrashBin)
        {
            Deny("страву можна віддати лише на видачу або викинути");
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
            case StationType.SupplyTerminal: HandleSupplyTerminal(chef); break;
        }
    }

    void HandleSupplyTerminal(ChefInteraction chef)
    {
        if (Inventory.Instance == null) { Deny("немає Inventory у сцені"); return; }

        bool ordered = Inventory.Instance.TryOrderSupply(supplyIngredient, out float eta);
        string label = Labels.Food(supplyIngredient);

        if (ordered)
        {
            Log($"замовив {supplyIngredient}, приліт за {eta:F0}с");
            Feedback.Sfx("buy");
            Feedback.Good($"Замовлено {label}, дрон прилетить за {eta:F0}с");
        }
        else if (eta > 0f)
            Deny($"{label} вже в дорозі, залишилось {eta:F0}с");
        else
            Deny($"{label} на складі й так повний");
    }

    // ---------- Джерело інгредієнтів ----------

    void HandleFridge(ChefInteraction chef)
    {
        if (chef.Hand != null) { Deny("руки зайняті"); return; }

        if (Inventory.Instance == null || !Inventory.Instance.TryTake(fridgeFood))
        {
            Deny($"{Labels.Food(fridgeFood)} закінчилось на складі");
            return;
        }

        chef.Hand = new FoodItem(fridgeFood, fridgeNeedsCutting);
        Log($"взяв {chef.Hand}, лишилось {Inventory.Instance.Current(fridgeFood)}");
    }

    // ---------- Дошка ----------
    // Правила:
    // - порожня дошка: кладемо продукт, що потребує нарізання;
    // - на дошці ненарізане, руки порожні: нарізаємо (2 або 3 натискання);
    // - на дошці нарізане, руки порожні: беремо;
    // - на дошці щось є, у руках продукт для нарізання: обмін.

    void HandleCutting(ChefInteraction chef)
    {
        // Порожня дошка
        if (_onTop == null)
        {
            if (chef.Hand == null) { Deny("дошка порожня, у руках нічого"); return; }
            if (!chef.Hand.needsCutting) { Deny($"{Labels.Item(chef.Hand)} не потребує нарізання"); return; }

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

        Deny("щоб забрати з дошки, руки мають бути порожні або тримати продукт для нарізання");
    }

    void PlaceOnBoard(FoodItem item)
    {
        _onTop = item;
        _cutPresses = 0;
    }

    void PressCut()
    {
        int need = GameSession.CutPresses;
        _cutPresses++;
        ShowCut(_cutPresses / (float)need, true);
        Log($"різання {_cutPresses}/{need}");

        // Тон росте з кожним натисканням, останнє закріплюється «дзвіночком»
        Feedback.Sfx("cut", 0.9f, 0.9f + 0.12f * _cutPresses);

        if (_cutPresses >= need)
        {
            _onTop.needsCutting = false;
            _onTop.state = FoodState.Cut;
            Feedback.Sfx("cutdone");
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

    int OpenSlotOrNone(ChefInteraction chef)
    {
        int n = Mathf.Min(GameSession.BurnerCount, _burners.Length);
        int slot = chef.TargetSlot;
        return (slot < 0 || slot >= n) ? -1 : slot;
    }

    int StovePickIndex(ChefInteraction chef)
    {
        int slot = OpenSlotOrNone(chef);
        return slot >= 0 ? slot : NearestBurner(chef.transform.position, true);
    }

    int StoveTargetIndex(ChefInteraction chef)
    {
        int slot = OpenSlotOrNone(chef);
        if (slot >= 0) return slot;

        int target = NearestBurner(chef.transform.position, false);      // спершу вільна
        if (target < 0)
            target = NearestBurner(chef.transform.position, true);       // усі зайняті: обмін
        return target;
    }

    void HandleStove(ChefInteraction chef)
    {
        // Руки порожні: беремо
        if (chef.Hand == null)
        {
            int idx = StovePickIndex(chef);
            if (idx < 0) { Deny("плита порожня"); return; }
            if (_burners[idx] == null) { Deny($"комфорка {idx} порожня"); return; }

            chef.Hand = _burners[idx];
            _burners[idx] = null;
            Log($"взяв {chef.Hand} з комфорки {idx}");
            return;
        }

        // Руки з їжею
        int target = StoveTargetIndex(chef);
        if (target < 0) { Deny("немає відкритих комфорок"); return; }

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
            if (_items.Count == 0) { Deny("сховище порожнє"); return; }

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

        if (_items.Count >= capacity) { Deny("сховище повне"); return; }

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
            if (_dish.parts.Count == 0)
            {
                Deny("тарілка порожня, нема чого брати");
                return;
            }

            chef.Hand = _dish;
            Log($"взяв страву {_dish}");
            _dish = new Dish();
            return;
        }

        if (_dish.parts.Count >= plateCapacity) { Deny("тарілка повна"); return; }

        _dish.parts.Add(chef.Hand);
        _dish.Refresh();
        Log($"додав {chef.Hand} у страву, {_dish.parts.Count}/{plateCapacity}");
        chef.Hand = null;
    }

    // ---------- Видача і смітник ----------

    void HandleServing(ChefInteraction chef)
    {
        if (chef.Hand == null) { Deny("руки порожні"); return; }

        if (!(chef.Hand is Dish dish))
        {
            Deny($"{Labels.Item(chef.Hand)} без тарілки не можна видати");
            return;
        }

        if (OrderManager.Instance == null)
        {
            Deny("немає OrderManager у сцені");
            return;
        }

        // Усі повідомлення про успіх чи відмову дає сам OrderManager
        if (OrderManager.Instance.TrySubmit(dish))
            chef.Hand = null;
    }

    void HandleTrash(ChefInteraction chef)
    {
        if (chef.Hand == null) { Deny("руки порожні"); return; }

        Log($"викинув {chef.Hand}");
        chef.Hand = null;
        Feedback.Sfx("trash");
    }

    // ---------- Очищення в кінці зміни ----------

    public void ClearContents()
    {
        _onTop = null;
        _cutPresses = 0;
        _items.Clear();
        System.Array.Clear(_burners, 0, _burners.Length);
        System.Array.Clear(_warnedFoodId, 0, _warnedFoodId.Length);

        if (type == StationType.AssemblyTable) _dish = new Dish();
    }

    // Лог + видиме гравцю повідомлення зі звуком помилки
    void Deny(string msg)
    {
        Log(msg);
        Feedback.Warn(Capitalize(msg));
    }

    static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    void Log(string msg) => Debug.Log($"[{name}] {msg}");
}