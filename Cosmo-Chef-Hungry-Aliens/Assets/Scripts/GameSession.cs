using System.Collections.Generic;
using UnityEngine;

// Стан між змінами: кредити, борг, куплені апгрейди
public static class GameSession
{
    public static int Credits { get; private set; }
    public static int Debt { get; private set; } = 300; // стартовий борг, підлаштуйте

    static readonly HashSet<string> owned = new HashSet<string>();

    public static void ResetAll()
    {
        Credits = 0;
        Debt = 300;
        owned.Clear();
    }

    public static void AddCredits(int amount) => Credits += amount;

    public static bool Owns(string id) => !string.IsNullOrEmpty(id) && owned.Contains(id);

    public static bool TryBuy(Upgrade u)
    {
        if (Owns(u.id) || !u.Unlocked || Credits < u.price) return false;

        Credits -= u.price;
        owned.Add(u.id);
        return true;
    }

    public static int PayDebt(int amount)
    {
        int pay = Mathf.Clamp(amount, 0, Mathf.Min(Debt, Credits));
        Credits -= pay;
        Debt -= pay;
        return pay;
    }

    // Робочих комфорок на плиті: одна базова плюс куплені
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