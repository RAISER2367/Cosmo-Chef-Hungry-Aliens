using System.Collections.Generic;
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

    readonly RaycastHit[] _hits = new RaycastHit[24];

    public int TargetSlot { get; private set; } = -1;

    public FoodItem Hand
    {
        get => _hand;
        set
        {
            if (ReferenceEquals(_hand, value)) return;
            _hand = value;
            Feedback.Sfx(value != null ? "pick" : "drop", 0.8f);
        }
    }

    // Тихе скидання (початок зміни, повернення в хаб): без звуку «поклав»
    public void ResetHand() => _hand = null;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
    }

    void Update()
    {
        UpdateHeldVisuals();

        if (!GameFlow.CanInteract)
        {
            Feedback.SetPrompt("");
            Feedback.SetHand("");
            return;
        }

        // Щокадрове наведення: підказка з'являється ще до натискання
        Station station;
        Laptop laptop;
        Aim(out station, out laptop);

        string prompt = "";
        bool actionable = true;

        if (GameFlow.Phase == GamePhase.Hub)
        {
            if (laptop != null) prompt = "Відкрити ноутбук";
        }
        else if (GameFlow.Phase == GamePhase.Shift && station != null)
        {
            prompt = station.GetPrompt(this, out actionable);
        }

        Feedback.SetPrompt(prompt, actionable);
        Feedback.SetHand(_hand != null ? Labels.Item(_hand) : "");

        bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
        if (!pressed) return;

        // Клік по кнопці UI, що щойно змінила фазу, не має стати дією у світі
        if (GameFlow.JustChanged) return;

        // У хабі E працює тільки на ноутбуку
        if (GameFlow.Phase == GamePhase.Hub)
        {
            if (laptop != null)
            {
                Feedback.Sfx("click");
                laptop.Use();
            }
            else Debug.Log("[Hub] дивіться на ноутбук");
            return;
        }

        // Зміна: звичайна логіка станцій
        if (station == null)
        {
            Debug.Log("Дивіться на станцію");
            return;
        }

        station.Interact(this);
    }

    // Один SphereCast на кадр без виділення пам'яті. Перше влучання (крім самого шефа) вирішує все.
    void Aim(out Station station, out Laptop laptop)
    {
        station = null;
        laptop = null;
        TargetSlot = -1;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        int n = Physics.SphereCastNonAlloc(
            ray, aimRadius, _hits, interactDistance, interactMask, QueryTriggerInteraction.Ignore);

        System.Array.Sort(_hits, 0, n, HitDistanceComparer.Instance);

        for (int i = 0; i < n; i++)
        {
            Collider c = _hits[i].collider;
            if (c.GetComponentInParent<ChefInteraction>() == this) continue;

            laptop = c.GetComponentInParent<Laptop>();

            StationSlot slot = c.GetComponent<StationSlot>();
            if (slot != null)
            {
                TargetSlot = slot.index;
                station = slot.station;
                return;
            }

            station = c.GetComponentInParent<Station>();
            return;
        }
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

    class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}