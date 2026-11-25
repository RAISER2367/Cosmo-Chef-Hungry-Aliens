using System.Collections.Generic;
using UnityEngine;

public enum Speaker { Dad, Mom }

public class DialogueNode
{
    public Speaker speaker;
    public string text;

    string Name => speaker == Speaker.Mom ? "Мама" : "Тато";
    public string Formatted => Name + ": " + text;
}

public class DialogueChoice
{
    public string label;
    public System.Func<DialogueNode> execute;   // виконує ефект і повертає відповідь
    public DialogueNode Pick() => execute();
}

public class ParentDialogueDirector : MonoBehaviour
{
    const int StartDebt = 300;
    const int BailoutDebt = 350;
    const int BailoutCreditsBelow = 20;
    const int BailoutAmount = 40;
    const int BragUpgrades = 3;
    const string FinaleReward = "cosm_photo";

    // Порядок = пріоритет
    static readonly (string upgradeId, Speaker who, string text)[] CosmeticReactions =
    {
        ("cosm_plant", Speaker.Mom, "О, у тебе в кабіні рослина! Тільки не забувай її поливати."),
        ("cosm_neon",  Speaker.Dad, "Що це за неон у тебе за спиною? Хоч клієнтів приманює?"),
        ("cosm_led",   Speaker.Mom, "Яка гарна підсвітка! Одразу видно, що там затишно."),
        ("cosm_music", Speaker.Dad, "Це в тебе музика на фоні? Непогано, тільки не на повну гучність."),
    };

    System.Random _rng;
    bool _tipUsedThisCall;

    System.Random Rng => _rng ??= new System.Random(PreferenceBook.Seed + 7);

    // ---------- Вхід у дзвінок ----------

    public DialogueNode BeginCall()
    {
        _tipUsedThisCall = false;
        return Finale() ?? CosmeticNode() ?? StageNode();
    }

    // Після погашення боргу: віха або фінал показуються одразу, а не при наступному дзвінку
    public DialogueNode AfterPayment(int paid)
    {
        DialogueNode n = Finale();
        if (n != null) return n;

        if (GameSession.Debt < 100 && !GameSession.HasSeen("stage3_enter"))
            return StageNode();

        return Say(
            (Speaker.Dad, $"Переказ на {paid} бачу. Лишилось {GameSession.Debt}."),
            (Speaker.Mom, $"Дякуємо, що платиш. Борг тепер {GameSession.Debt}."));
    }

    // ---------- Кнопки вибору (0..2) ----------

    public List<DialogueChoice> GetChoices()
    {
        var list = new List<DialogueChoice>(2);

        if (!_tipUsedThisCall)
            list.Add(new DialogueChoice { label = "Запитати пораду перед зміною", execute = MomTip });

        if (CanBailout())
            list.Add(new DialogueChoice { label = "Попросити виручити на закупівлю", execute = Bailout });
        else if (CanBrag())
            list.Add(new DialogueChoice { label = "Похвалитися кухнею", execute = Brag });

        return list;
    }

    // ---------- Сюжетні етапи ----------

    DialogueNode StageNode()
    {
        int d = GameSession.Debt;

        if (d <= 0)
            return Say(
                (Speaker.Dad, "Пишаюся тобою. Тепер це твій бізнес, вирішуй сам."),
                (Speaker.Mom, "Не забувай нам дзвонити, навіть коли борг нульовий."));

        if (d < 100)
            return OneShot("stage3_enter", Speaker.Dad, $"Лишилось лише {d}! Ще трохи, і фудтрак твій.")
                ?? Say(
                    (Speaker.Mom, $"Лишилось {d}. Ти майже на фініші!"),
                    (Speaker.Dad, $"{d} до кінця. Не розслабляйся."));

        if (d > StartDebt)
            return Say(
                (Speaker.Dad, $"Я бачив виписку з банку: борг виріс до {d}. Закупівлі в мінус? Фінансова дисципліна, синку."),
                (Speaker.Dad, $"Борг {d}, а починали з {StartDebt}. Не бери в кредит те, що можна не купувати."));

        if (d == StartDebt && GameSession.ShiftsPlayed == 0)
            return OneShot("intro", Speaker.Mom,
                    $"Синку, ми з татом заклали заощадження на старий фудтрак. Ти винен нам {d}, і відсотки йдуть, тож не затягуй.")
                ?? Say(
                    (Speaker.Dad, "Базова порада: порахуй собівартість страви до зміни й не тримай склад на нулі."),
                    (Speaker.Mom, "Дивись у блокнот перед зміною. Смаки клієнтів не змінюються."));

        if (d == StartDebt)
            return Say(
                (Speaker.Mom, $"Борг усе ще {d}. Перші зміни завжди важкі, не здавайся."),
                (Speaker.Dad, "Головне, щоб борг не ріс. Решта прийде з досвідом."));

        // 100..299
        return Say(
            (Speaker.Mom, $"Борг уже {d}. Ми з татом бачимо, що ти стараєшся."),
            (Speaker.Dad, $"Лишилось {d}. Так тримати."));
    }

    DialogueNode Finale()
    {
        if (GameSession.Debt > 0 || GameSession.HasSeen("finale")) return null;

        GameSession.MarkSeen("finale");
        GameSession.Grant(FinaleReward);

        return Node(Speaker.Mom,
            "Борг закрито! Фудтрак тепер повністю твій. Ми надіслали тобі невеликий сувенір у кабіну, подивись на панель.");
    }

    DialogueNode CosmeticNode()
    {
        foreach (var c in CosmeticReactions)
        {
            string seenId = "cosm_react_" + c.upgradeId;
            if (GameSession.Owns(c.upgradeId) && !GameSession.HasSeen(seenId))
                return OneShot(seenId, c.who, c.text);
        }
        return null;
    }

    // ---------- Дії кнопок ----------

    // Зв'язок із темою 1: один реальний Likes/Loves випадкового виду
    DialogueNode MomTip()
    {
        _tipUsedThisCall = true;

        SpeciesDefinition sp = PreferenceBook.Species[Rng.Next(PreferenceBook.Species.Length)];
        var liked = new List<(string food, FoodState state, PreferenceLevel level)>();

        foreach (var e in PreferenceBook.GetAll(sp.id))
            if (e.level == PreferenceLevel.Likes || e.level == PreferenceLevel.Loves)
                liked.Add(e);

        if (liked.Count == 0)
            return Node(Speaker.Mom, $"Про «{sp.displayName}» нічого певного не скажу, придивись до нього сам.");

        var tip = liked[Rng.Next(liked.Count)];
        string verb = tip.level == PreferenceLevel.Loves ? "обожнює" : "любить";

        return Node(Speaker.Mom,
            $"Запиши: «{sp.displayName}» {verb} {FoodLabel(tip.food)} зі станом «{StateLabel(tip.state)}».");
    }

    // Зв'язок із темою 2: одноразова страховка
    bool CanBailout() =>
        !GameSession.HasSeen("dad_bailout")
        && GameSession.Debt > BailoutDebt
        && GameSession.Credits < BailoutCreditsBelow;

    DialogueNode Bailout()
    {
        if (!CanBailout()) return Node(Speaker.Dad, "Я вже допомагав. Далі сам.");

        GameSession.MarkSeen("dad_bailout");
        GameSession.GrantCredits(BailoutAmount);
        return Node(Speaker.Dad, $"*бурчить* Ну добре. Тримай {BailoutAmount}, але це востаннє. Рахуй гроші, синку.");
    }

    bool CanBrag() =>
        !GameSession.HasSeen("brag") && FunctionalUpgradesOwned() >= BragUpgrades;

    DialogueNode Brag()
    {
        GameSession.MarkSeen("brag");
        return Say(
            (Speaker.Dad, "Лазерний ніж, дрони… Мама питає, коли ти купиш космічну ракету. Тільки без нових боргів, добре?"),
            (Speaker.Mom, "Бачу, кухня тепер як у справжніх шефів! Тільки не забувай і про борг."));
    }

    static int FunctionalUpgradesOwned()
    {
        int n = 0;
        foreach (var u in UpgradeCatalog.All)
            if (!u.isCosmetic && GameSession.Owns(u.id)) n++;
        return n;
    }

    // ---------- Хелпери ----------

    static DialogueNode Node(Speaker who, string text) => new DialogueNode { speaker = who, text = text };

    DialogueNode Say(params (Speaker who, string text)[] variants)
    {
        var v = variants[Rng.Next(variants.Length)];
        return Node(v.who, v.text);
    }

    // Повертає null, якщо вже було
    static DialogueNode OneShot(string id, Speaker who, string text)
    {
        if (GameSession.HasSeen(id)) return null;
        GameSession.MarkSeen(id);
        return Node(who, text);
    }

    static string FoodLabel(string food) => food switch
    {
        "meat" => "м'ясо",
        "cucumber" => "огірок",
        _ => food
    };

    static string StateLabel(FoodState s) => s switch
    {
        FoodState.Raw => "сирий",
        FoodState.Cut => "нарізаний",
        FoodState.Ready => "готовий",
        FoodState.Burnt => "підгорілий",
        _ => s.ToString()
    };
}
