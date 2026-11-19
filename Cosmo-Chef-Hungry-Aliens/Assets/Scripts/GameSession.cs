using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameSession
{
    public static event Action OnBalanceChanged;

    public static int Credits { get; private set; }
    public static int Debt { get; private set; } = 300;

    static readonly HashSet<string> owned = new HashSet<string>();

    public static void ResetAll()
    {
        Credits = 0;
        Debt = 300;
        owned.Clear();

        int seed = System.Environment.TickCount;
        PreferenceBook.Generate(seed);
        Debug.Log($"[Preferences] згенеровано з seed {seed}");

        OnBalanceChanged?.Invoke();
    }

    public static void AddCredits(int amount)
    {
        Credits += amount;
        OnBalanceChanged?.Invoke();
    }

    public static void SpendOrDebt(int cost)
    {
        int fromCredits = Mathf.Min(Credits, cost);
        Credits -= fromCredits;
        Debt += cost - fromCredits;
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
        if (pay > 0) OnBalanceChanged?.Invoke();
        return pay;
    }

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
}