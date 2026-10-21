using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeRow : MonoBehaviour
{
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text description;
    [SerializeField] Button button;
    [SerializeField] TMP_Text buttonText;

    public void Setup(Upgrade u, System.Action onBuy)
    {
        title.text = u.title;
        description.text = u.description;
        button.onClick.RemoveAllListeners();

        if (GameSession.Owns(u.id))
        {
            buttonText.text = "Куплено";
            button.interactable = false;
            return;
        }

        if (!u.Unlocked)
        {
            buttonText.text = "Спочатку попереднє";
            button.interactable = false;
            return;
        }

        if (GameSession.Credits < u.price)
        {
            buttonText.text = $"Бракує {u.price - GameSession.Credits}";
            button.interactable = false;
            return;
        }

        buttonText.text = $"Купити · {u.price}";
        button.interactable = true;
        button.onClick.AddListener(() => onBuy());
    }
}