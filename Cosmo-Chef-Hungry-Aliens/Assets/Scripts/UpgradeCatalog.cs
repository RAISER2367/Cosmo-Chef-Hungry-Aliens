using System.Collections.Generic;

public enum UpgradeCategory { Kitchen, Logistics, Finance, Cosmetic }

public class Upgrade
{
    public readonly string id, title, description, requires;
    public readonly int price;
    public readonly UpgradeCategory category;
    public readonly bool isCosmetic;

    public Upgrade(string id, string title, string description, int price,
        UpgradeCategory category, string requires = null)
    {
        this.id = id; this.title = title; this.description = description;
        this.price = price; this.category = category; this.requires = requires;
        isCosmetic = category == UpgradeCategory.Cosmetic;
    }

    public bool Unlocked => string.IsNullOrEmpty(requires) || GameSession.Owns(requires);
}

public static class UpgradeCatalog
{
    public static readonly Upgrade[] All =
    {
        // Кухня
        new Upgrade("burner2", "Друга комфорка", "Ще одне місце на плиті", 80, UpgradeCategory.Kitchen),
        new Upgrade("burner3", "Третя комфорка", "Плита на три місця", 150, UpgradeCategory.Kitchen, "burner2"),
        new Upgrade("burner4", "Суцільна плита", "Чотири місця одночасно", 250, UpgradeCategory.Kitchen, "burner3"),
        new Upgrade("laser_knife", "Лазерний ніж", "2 натискання замість 3 для нарізання", 130, UpgradeCategory.Kitchen),
        new Upgrade("induction_coil", "Індукційний контур", "Вікно до Burnt: 9с → 12с", 140, UpgradeCategory.Kitchen),

        // Склад і логістика
        new Upgrade("fridge_cap1", "Більший холодильник I", "+10 до місткості", 120, UpgradeCategory.Logistics),
        new Upgrade("fridge_cap2", "Більший холодильник II", "Ще +10 до місткості", 220, UpgradeCategory.Logistics, "fridge_cap1"),
        new Upgrade("turbo_drone1", "Турбо-дрони I", "Доставка 15с → 10с", 160, UpgradeCategory.Logistics),
        new Upgrade("turbo_drone2", "Турбо-дрони II", "Доставка 10с → 6с", 260, UpgradeCategory.Logistics, "turbo_drone1"),
        new Upgrade("autobuy1", "Автологістика I", "Замовляє при запасі < 12%", 150, UpgradeCategory.Logistics),
        new Upgrade("autobuy2", "Автологістика II", "Замовляє при запасі < 50%", 260, UpgradeCategory.Logistics, "autobuy1"),
        new Upgrade("magnetic_clamps", "Магнітні фіксатори", "Захист столів від турбулентності", 180, UpgradeCategory.Logistics),

        // Фінанси
        new Upgrade("debt_restructure", "Реструктуризація боргу", "Відсоток на борг: 12% → 6%", 300, UpgradeCategory.Finance),
        new Upgrade("tip_terminal", "Термінал чайових", "+15% до бонусів vip і швидкої видачі", 220, UpgradeCategory.Finance),

        // Косметика
        new Upgrade("cosm_plant", "Кімнатна рослина", "Трохи затишку в кабіні", 40, UpgradeCategory.Cosmetic),
        new Upgrade("cosm_neon", "Неонова вивіска", "Світиться у вікні", 60, UpgradeCategory.Cosmetic),
        new Upgrade("cosm_rug", "Килимок біля плити", "М'яке покриття під ногами", 30, UpgradeCategory.Cosmetic),
        new Upgrade("cosm_led", "LED-підсвітка кабіни", "Неонове освітлення робочої зони", 70, UpgradeCategory.Cosmetic),
        new Upgrade("cosm_music", "Космічний програвач", "Фонова музика", 50, UpgradeCategory.Cosmetic),
        new Upgrade("cosm_shelf", "Декоративні полиці", "Стильні полиці в салоні", 45, UpgradeCategory.Cosmetic),
    };

    public static IEnumerable<Upgrade> InCategory(UpgradeCategory c)
    {
        foreach (var u in All) if (u.category == c) yield return u;
    }
}