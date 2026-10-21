public class Upgrade
{
    public readonly string id;
    public readonly string title;
    public readonly string description;
    public readonly int price;
    public readonly string requires;

    public Upgrade(string id, string title, string description, int price, string requires = null)
    {
        this.id = id;
        this.title = title;
        this.description = description;
        this.price = price;
        this.requires = requires;
    }

    public bool Unlocked => string.IsNullOrEmpty(requires) || GameSession.Owns(requires);
}

public static class UpgradeCatalog
{
    public static readonly Upgrade[] All =
    {
        new Upgrade("burner2", "Друга комфорка", "Ще одне місце для приготування на плиті", 80),
        new Upgrade("burner3", "Третя комфорка", "Плита на три місця", 150, "burner2"),
        new Upgrade("burner4", "Суцільна плита", "Чотири місця, можна готувати одразу кілька страв", 250, "burner3"),
    };
}