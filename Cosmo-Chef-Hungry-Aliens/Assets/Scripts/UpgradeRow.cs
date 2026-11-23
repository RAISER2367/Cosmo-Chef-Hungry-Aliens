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

    public void Setup(Upgrade u, Action onBuy)
    {
        titleText.text = u.title;
        descriptionText.text = u.description;

        bool owned = GameSession.Owns(u.id);
        bool unlocked = u.Unlocked;
        bool canAfford = GameSession.Credits >= u.price;

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
            buttonText.text = $"{u.price}";
            buyButton.interactable = canAfford;
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke());
    }
}