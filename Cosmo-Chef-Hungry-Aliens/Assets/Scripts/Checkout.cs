using System.Collections.Generic;
using UnityEngine;

public static class Checkout
{
    public struct Result
    {
        public bool refused;
        public float payFactor;
        public string reason;
    }

    public static Result Evaluate(string speciesId, Dish dish, float patienceFraction)
    {
        var levels = new List<PreferenceLevel>();
        foreach (var part in dish.parts)
            levels.Add(PreferenceBook.GetPreference(speciesId, part.foodName, part.state));

        if (levels.Contains(PreferenceLevel.Hates))
            return new Result { refused = true, payFactor = 0f, reason = "ненавидить один з компонентів" };

        bool hasLoves = levels.Contains(PreferenceLevel.Loves);
        bool hasDislikes = levels.Contains(PreferenceLevel.Dislikes);
        bool speedBonus = patienceFraction > 0.65f;

        float factor = speciesId switch
        {
            "vip" when hasDislikes => 0.5f,
            "vip" when hasLoves && !hasDislikes => 1.35f,
            "grump" when hasLoves && speedBonus => 1.0f,
            "grump" => 0.85f,
            _ => 1.0f
        };

        // Швидкість не подвоюється для grump: у нього вже є власне правило на Loves+speed
        if (speedBonus && speciesId != "grump") factor *= 1.2f;

        return new Result { refused = false, payFactor = factor, reason = $"фактор x{factor:F2}" };
    }

    // Собівартість + націнка закладу
    public static int BaseCost(Dish dish, float markup)
    {
        int sum = 0;
        foreach (var part in dish.parts)
            sum += Inventory.Instance != null ? Inventory.Instance.PricePerUnit(part.foodName) : 2;

        return Mathf.RoundToInt(sum * markup);
    }
}