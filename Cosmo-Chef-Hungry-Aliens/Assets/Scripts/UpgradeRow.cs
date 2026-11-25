using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeRow : MonoBehaviour
{
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] Button buyButton;
    [SerializeField] TMP_Text buttonText;

    static readonly Color AffordColor = Color.white;
    static readonly Color TooPoorColor = new Color(1f, 0.45f, 0.4f);

    public void Setup(Upgrade u, Action onBuy)
    {
        titleText.text = u.title;
        descriptionText.text = u.description;

        bool owned = GameSession.Owns(u.id);
        bool unlocked = u.Unlocked;
        bool canAfford = GameSession.Credits >= u.price;

        buttonText.color = AffordColor;

        if (owned)
        {
            buttonText.text = "Куплено";
            buyButton.interactable = false;
        }
        else if (!unlocked)
        {
            buttonText.text = "Закрито";
            buyButton.interactable = false;
        }
        else
        {
            // Кнопка лишається активною навіть без грошей: натискання дає пояснення («Не вистачає N кредитів»),
            // а червона ціна показує, що зараз не по кишені
            buttonText.text = $"{u.price}";
            buttonText.color = canAfford ? AffordColor : TooPoorColor;
            buyButton.interactable = true;
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke());
    }
}