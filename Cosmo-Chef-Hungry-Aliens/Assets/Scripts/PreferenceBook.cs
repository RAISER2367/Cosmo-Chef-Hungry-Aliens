using System;
using System.Collections.Generic;
using UnityEngine;

public enum PreferenceLevel { Hates, Dislikes, Neutral, Likes, Loves }

[System.Serializable]
public class SpeciesDefinition
{
    public string id;
    public string displayName;
    public float patienceSeconds;
    public float moveSpeed;
    public int minIngredients;
    public int maxIngredients;
    public float rewardMultiplier;
}

public static class PreferenceBook
{
    public static readonly SpeciesDefinition[] Species =
    {
        new SpeciesDefinition { id = "rookie", displayName = "Новачок",    patienceSeconds = 45f, moveSpeed = 1.6f, minIngredients = 1, maxIngredients = 2, rewardMultiplier = 1.0f },
        new SpeciesDefinition { id = "vip",    displayName = "Вибагливий", patienceSeconds = 55f, moveSpeed = 1.3f, minIngredients = 2, maxIngredients = 3, rewardMultiplier = 1.5f },
        new SpeciesDefinition { id = "grump",  displayName = "Буркотун",   patienceSeconds = 30f, moveSpeed = 2.0f, minIngredients = 1, maxIngredients = 3, rewardMultiplier = 1.2f },
    };

    // Єдине джерело правди про те, які стани фізично можливі для кожного продукту
    static readonly Dictionary<string, FoodState[]> _validStates = new Dictionary<string, FoodState[]>
    {
        { "meat",     new[] { FoodState.Raw, FoodState.Ready, FoodState.Burnt } },
        { "cucumber", new[] { FoodState.Raw, FoodState.Cut, FoodState.Ready, FoodState.Burnt } },
    };

    public static IEnumerable<string> Foods => _validStates.Keys;

    public static FoodState[] StatesOf(string food) =>
        _validStates.TryGetValue(food, out var s) ? s : Array.Empty<FoodState>();

    public static bool IsValidCombination(string food, FoodState state) =>
        _validStates.TryGetValue(food, out var states) && Array.IndexOf(states, state) >= 0;

    // speciesId -> (food, state) -> рівень
    static Dictionary<string, Dictionary<(string, FoodState), PreferenceLevel>> _table;

    public static int Seed { get; private set; }

    public static void Generate(int seed)
    {
        Seed = seed;
        var rng = new System.Random(seed);
        _table = new Dictionary<string, Dictionary<(string, FoodState), PreferenceLevel>>();

        foreach (var sp in Species)
        {
            var map = new Dictionary<(string, FoodState), PreferenceLevel>();

            foreach (string food in Foods)
            {
                foreach (FoodState state in StatesOf(food))
                {
                    if (state == FoodState.Burnt)
                    {
                        map[(food, state)] = PreferenceLevel.Hates;
                        continue;
                    }

                    map[(food, state)] = (PreferenceLevel)rng.Next(0, 5);
                }
            }

            EnsureSolvablePerFood(map, rng);
            EnsureVariety(map, rng);
            _table[sp.id] = map;
        }
    }

    // Гарантує, що для кожного продукту є хоча б один стан >= Neutral (без штрафу)
    static void EnsureSolvablePerFood(Dictionary<(string, FoodState), PreferenceLevel> map, System.Random rng)
    {
        foreach (string food in Foods)
        {
            var valid = new List<FoodState>();
            bool hasAcceptable = false;

            foreach (FoodState state in StatesOf(food))
            {
                if (state == FoodState.Burnt) continue;
                valid.Add(state);
                if (map[(food, state)] >= PreferenceLevel.Neutral)
                    hasAcceptable = true;
            }

            if (!hasAcceptable && valid.Count > 0)
            {
                FoodState chosen = valid[rng.Next(valid.Count)];
                map[(food, chosen)] = (PreferenceLevel)rng.Next(2, 5); // Neutral, Likes або Loves
            }
        }
    }

    // Гарантує наявність хоча б одного Loves і одного Hates (крім Burnt) у кожного виду
    static void EnsureVariety(Dictionary<(string, FoodState), PreferenceLevel> map, System.Random rng)
    {
        bool hasLoves = false, hasHates = false;
        var candidates = new List<(string, FoodState)>();

        foreach (var kv in map)
        {
            if (kv.Key.Item2 == FoodState.Burnt) continue;
            candidates.Add(kv.Key);
            if (kv.Value == PreferenceLevel.Loves) hasLoves = true;
            if (kv.Value == PreferenceLevel.Hates) hasHates = true;
        }

        if (candidates.Count == 0) return;
        if (!hasLoves) map[candidates[rng.Next(candidates.Count)]] = PreferenceLevel.Loves;

        if (!hasHates)
        {
            var safeForHates = candidates.FindAll(k =>
            {
                if (map[k] == PreferenceLevel.Loves) return false;
                foreach (var other in candidates)
                {
                    if (other.Item1 == k.Item1 && other.Item2 != k.Item2 && map[other] >= PreferenceLevel.Neutral)
                        return true;
                }
                return false;
            });

            if (safeForHates.Count > 0)
                map[safeForHates[rng.Next(safeForHates.Count)]] = PreferenceLevel.Hates;
        }
    }

    public static PreferenceLevel GetPreference(string speciesId, string food, FoodState state)
    {
        if (_table == null || !_table.TryGetValue(speciesId, out var map)) return PreferenceLevel.Neutral;
        return map.TryGetValue((food, state), out var level) ? level : PreferenceLevel.Neutral;
    }

    public static IEnumerable<(string food, FoodState state, PreferenceLevel level)> GetAll(string speciesId)
    {
        if (_table == null || !_table.TryGetValue(speciesId, out var map)) yield break;
        foreach (var kv in map)
            yield return (kv.Key.Item1, kv.Key.Item2, kv.Value);
    }

    public static SpeciesDefinition Get(string id)
    {
        foreach (var s in Species) if (s.id == id) return s;
        return Species[0];
    }
}