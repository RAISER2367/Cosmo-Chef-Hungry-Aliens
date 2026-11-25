using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameSession
{
    public static event Action OnBalanceChanged;

    public static int Credits { get; private set; }
    public static int Debt { get; private set; } = 300;

    // Фінансова розбивка поточної зміни — читає HubUI.ShowResult
    public static int ShiftRevenue { get; private set; }
    public static int ShiftExpenses { get; private set; }
    public static int ShiftInterest { get; private set; }

    static readonly HashSet<string> owned = new HashSet<string>();

    /// <summary>
    /// Dialogue
    /// </summary>
    public static int ShiftsPlayed { get; private set; }
    static readonly HashSet<string> seen = new HashSet<string>();

    public static bool HasSeen(string id) => seen.Contains(id);
    public static void MarkSeen(string id) => seen.Add(id);
    public static void RegisterShiftPlayed() => ShiftsPlayed++;

    // Безкоштовне видання предмета (не купівля)
    public static void Grant(string id)
    {
        if (owned.Add(id)) OnBalanceChanged?.Invoke();
    }

    // Допомога батьків: гроші не вважаються виручкою зміни
    public static void GrantCredits(int amount)
    {
        Credits += amount;
        OnBalanceChanged?.Invoke();
    }

    public static void ResetAll()
    {
        Credits = 0;
        Debt = 300;
        owned.Clear();

        int seed = Environment.TickCount;
        PreferenceBook.Generate(seed);
        Debug.Log($"[Preferences] згенеровано з seed {seed}");

        ShiftsPlayed = 0;
        seen.Clear();

        ShiftRevenue = 0;
        ShiftExpenses = 0;
        ShiftInterest = 0;

        OnBalanceChanged?.Invoke();
    }

    // Викликати на початку кожної зміни (OrderManager.StartShift)
    public static void BeginShiftAccounting()
    {
        ShiftRevenue = 0;
        ShiftExpenses = 0;
        ShiftInterest = 0;
    }

    public static void AddCredits(int amount)
    {
        Credits += amount;
        ShiftRevenue += amount;
        OnBalanceChanged?.Invoke();
    }

    public static bool Owns(string id) => !string.IsNullOrEmpty(id) && owned.Contains(id);

    public static bool TryBuy(Upgrade u)
    {
        if (Owns(u.id) || !u.Unlocked || Credits < u.price) return false;

        Credits -= u.price;
        owned.Add(u.id);
        OnBalanceChanged?.Invoke();
        return true;
    }

    public static int PayDebt(int amount)
    {
        int pay = Mathf.Clamp(amount, 0, Mathf.Min(Debt, Credits));
        Credits -= pay;
        Debt -= pay;
        OnBalanceChanged?.Invoke();
        return pay;
    }

    // Витрата коштів: якщо кредитів не вистачає, різниця йде в борг
    public static void SpendOrDebt(int cost)
    {
        int fromCredits = Mathf.Min(Credits, cost);
        int toDebt = cost - fromCredits;

        Credits -= fromCredits;
        Debt += toDebt;
        ShiftExpenses += cost;

        if (toDebt > 0) Feedback.Warn($"Кредитів не вистачило: борг +{toDebt}");

        OnBalanceChanged?.Invoke();
    }

    // Нараховується наприкінці кожного ігрового дня на багатоденних змінах
    public static void ApplyDailyInterest(float rate)
    {
        if (Debt <= 0) return;

        int interest = Mathf.CeilToInt(Debt * rate);
        Debt += interest;
        ShiftInterest += interest;
        Debug.Log($"[Debt] нараховано відсотки: +{interest}, борг тепер {Debt}");
        Feedback.Toast($"Новий день: відсотки +{interest} до боргу", new Color(1f, 0.75f, 0.4f));
        OnBalanceChanged?.Invoke();
    }

    // ---------- Параметри, які змінюють апгрейди ----------

    // Комфорок на плиті: одна базова плюс куплені
    public static int BurnerCount
    {
        get
        {
            int n = 1;
            if (Owns("burner2")) n++;
            if (Owns("burner3")) n++;
            if (Owns("burner4")) n++;
            return n;
        }
    }

    // Лазерний ніж: 3 натискання → 2
    public static int CutPresses => Owns("laser_knife") ? 2 : 3;

    // Індукційний контур: час до Burnt 9с → 12с
    public static float BurnTime => Owns("induction_coil") ? 12f : 9f;

    // Магнітні фіксатори: множник зносу й тряски в турбулентності
    public static float TurbulenceResistance => Owns("magnetic_clamps") ? 0.4f : 1f;

    public static int FridgeCapacityBonus
    {
        get { int b = 0; if (Owns("fridge_cap1")) b += 10; if (Owns("fridge_cap2")) b += 10; return b; }
    }

    public static float AutoBuyThreshold =>
        Owns("autobuy2") ? 0.5f : Owns("autobuy1") ? 0.125f : 0f;

    // Турбо-дрони скорочують базовий час доставки кожного інгредієнта (−33% і −60%)
    public static float TurboDroneLeadTime(float baseLeadTime) =>
        Owns("turbo_drone2") ? baseLeadTime * 0.4f : Owns("turbo_drone1") ? baseLeadTime * 0.67f : baseLeadTime;

    public static float DebtInterestRate => Owns("debt_restructure") ? 0.06f : 0.12f;

    public static float TipMultiplierBonus => Owns("tip_terminal") ? 1.15f : 1f;
}