using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class BalanceHUD : MonoBehaviour
{
    TMP_Text _text;

    void Awake() => _text = GetComponent<TMP_Text>();

    void OnEnable()
    {
        GameSession.OnBalanceChanged += Refresh;
        Refresh();
    }

    void OnDisable() => GameSession.OnBalanceChanged -= Refresh;

    void Refresh()
    {
        _text.text = $"Кредити: {GameSession.Credits}\nБорг: {GameSession.Debt}";
    }
}