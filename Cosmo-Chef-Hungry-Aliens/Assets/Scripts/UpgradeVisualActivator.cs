using UnityEngine;

public class UpgradeVisualActivator : MonoBehaviour
{
    [SerializeField] string upgradeId;
    [SerializeField] GameObject target;   // модель, яку вмикаємо

    void OnEnable()
    {
        GameSession.OnBalanceChanged += Refresh;
        Refresh();
    }

    void OnDisable() => GameSession.OnBalanceChanged -= Refresh;

    void Refresh()
    {
        if (target != null) target.SetActive(GameSession.Owns(upgradeId));
    }
}