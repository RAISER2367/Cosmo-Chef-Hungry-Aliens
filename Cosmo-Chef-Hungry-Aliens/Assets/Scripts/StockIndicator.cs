using TMPro;
using UnityEngine;

public class StockIndicator : MonoBehaviour
{
    [SerializeField] string ingredientId = "meat";
    [SerializeField] TMP_Text label;
    [SerializeField] int warningThreshold = 3;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color warningColor = new Color(1f, 0.3f, 0.2f);

    void Update()
    {
        if (Inventory.Instance == null || label == null) return;

        int current = Inventory.Instance.Current(ingredientId);
        int capacity = Inventory.Instance.Capacity(ingredientId);
        float pending = Inventory.Instance.PendingSeconds(ingredientId);

        label.text = pending >= 0f
            ? $"{current}/{capacity}\nдоставка: {pending:F0}с"
            : $"{current}/{capacity}";

        label.color = current <= warningThreshold ? warningColor : normalColor;
    }
}