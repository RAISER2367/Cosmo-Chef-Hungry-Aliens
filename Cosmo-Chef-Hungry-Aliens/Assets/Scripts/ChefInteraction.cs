using UnityEngine;

public class ChefInteraction : MonoBehaviour
{
    [SerializeField] Camera playerCamera;
    [SerializeField] float interactDistance = 1.8f;
    [SerializeField] float aimRadius = 0.1f;
    [SerializeField] LayerMask interactMask = ~0;

    [Header("Одна їжа в руках")]
    [SerializeField] FoodVisual heldFood;

    [Header("Страва в руках (тарілка + компоненти)")]
    [SerializeField] GameObject heldDishRoot;
    [SerializeField] FoodVisual[] heldDishSlots;

    FoodItem _hand;
    Station _target;
    string _lastHit = "нічого";

    public int TargetSlot { get; private set; } = -1;

    public FoodItem Hand
    {
        get => _hand;
        set => _hand = value;
    }

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
    }

    void Update()
    {
        UpdateHeldVisuals();

        if (!GameFlow.CanInteract) return;

        bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
        if (!pressed) return;

        // У хабі E працює тільки на ноутбуку
        if (GameFlow.Phase == GamePhase.Hub)
        {
            Laptop laptop = FindLaptop();
            if (laptop != null) laptop.Use();
            else Debug.Log("[Hub] дивіться на ноутбук");
            return;
        }

        // Зміна: звичайна логіка станцій
        _target = FindTargetStation();
        Debug.Log($"Натиск. Промінь влучив у: {_lastHit}");

        if (_target == null)
        {
            Debug.Log("Дивіться на станцію");
            return;
        }

        _target.Interact(this);
    }

    Laptop FindLaptop()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.SphereCastAll(
            ray, aimRadius, interactDistance, interactMask, QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<ChefInteraction>() == this) continue;
            return h.collider.GetComponentInParent<Laptop>();
        }
        return null;
    }

    void UpdateHeldVisuals()
    {
        bool isDish = _hand is Dish;

        // Звичайна їжа в руці: показуємо тільки якщо це не страва
        if (heldFood != null) heldFood.Apply(isDish ? null : _hand);

        // Страва: показуємо тарілку і компоненти, що на ній
        if (heldDishRoot != null) heldDishRoot.SetActive(isDish);

        if (isDish && heldDishSlots != null)
        {
            var dish = (Dish)_hand;
            for (int i = 0; i < heldDishSlots.Length; i++)
                heldDishSlots[i].Apply(i < dish.parts.Count ? dish.parts[i] : null);
        }
    }

    Station FindTargetStation()
    {
        TargetSlot = -1;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.SphereCastAll(
            ray, aimRadius, interactDistance, interactMask, QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<ChefInteraction>() == this) continue;

            _lastHit = h.collider.name;

            StationSlot slot = h.collider.GetComponent<StationSlot>();
            if (slot != null)
            {
                TargetSlot = slot.index;
                return slot.station;
            }

            return h.collider.GetComponentInParent<Station>();
        }

        _lastHit = "нічого";
        return null;
    }
}