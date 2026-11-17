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
        new SpeciesDefinition { id = "rookie", displayName = "Новачок",   patienceSeconds = 45f, moveSpeed = 1.6f, minIngredients = 1, maxIngredients = 2, rewardMultiplier = 1.0f },
        new SpeciesDefinition { id = "vip",     displayName = "Вибагливий", patienceSeconds = 55f, moveSpeed = 1.3f, minIngredients = 2, maxIngredients = 3, rewardMultiplier = 1.5f },
        new SpeciesDefinition { id = "grump",   displayName = "Буркотун",  patienceSeconds = 30f, moveSpeed = 2.0f, minIngredients = 1, maxIngredients = 3, rewardMultiplier = 1.2f },
    };

    static readonly string[] _foods = { "meat", "cucumber" };
    static readonly FoodState[] _states = { FoodState.Raw, FoodState.Cut, FoodState.Ready, FoodState.Burnt };

    // speciesId -> (food, state) -> рівень
    static Dictionary<string, Dictionary<(string, FoodState), PreferenceLevel>> _table;

    public static int Seed { get; private set; }

    // Викликається один раз на старті гри (GameSession.ResetAll)
    public static void Generate(int seed)
    {
        Seed = seed;
        var rng = new System.Random(seed);
        _table = new Dictionary<string, Dictionary<(string, FoodState), PreferenceLevel>>();

        foreach (var sp in Species)
        {
            var map = new Dictionary<(string, FoodState), PreferenceLevel>();

            foreach (var food in _foods)
            {
                foreach (var state in _states)
                {
                    // Підгоріле ніхто не любить, незалежно від виду
                    if (state == FoodState.Burnt)
                    {
                        map[(food, state)] = PreferenceLevel.Hates;
                        continue;
                    }

                    PreferenceLevel level = (PreferenceLevel)rng.Next(0, 5); // Hates..Loves
                    map[(food, state)] = level;
                }
            }

            // Гарантуємо, що в кожного виду є хоч один Loves і один Hates (крім Burnt),
            // інакше блокнот виглядає одноманітно
            EnsureVariety(map, rng);

            _table[sp.id] = map;
        }
    }

    static void EnsureVariety(Dictionary<(string, FoodState), PreferenceLevel> map, System.Random rng)
    {
        bool hasLoves = false, hasHates = false;
        List<(string, FoodState)> keys = new List<(string, FoodState)>(map.Keys);

        foreach (var k in keys)
        {
            if (k.Item2 == FoodState.Burnt) continue;
            if (map[k] == PreferenceLevel.Loves) hasLoves = true;
            if (map[k] == PreferenceLevel.Hates) hasHates = true;
        }

        if (!hasLoves) map[keys[rng.Next(keys.Count)]] = PreferenceLevel.Loves;
        if (!hasHates) map[keys[rng.Next(keys.Count)]] = PreferenceLevel.Hates;
    }

    public static PreferenceLevel GetPreference(string speciesId, string food, FoodState state)
    {
        if (_table == null || !_table.TryGetValue(speciesId, out var map)) return PreferenceLevel.Neutral;
        return map.TryGetValue((food, state), out var level) ? level : PreferenceLevel.Neutral;
    }

    // Для UI блокнота: повний список рядків для одного виду
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