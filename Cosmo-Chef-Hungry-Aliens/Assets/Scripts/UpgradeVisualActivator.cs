using UnityEngine;

public class UpgradeVisualActivator : MonoBehaviour
{
    [SerializeField] string upgradeId;

    void Start() => gameObject.SetActive(GameSession.Owns(upgradeId));

    void OnEnable() => GameSession.OnBalanceChanged += Refresh;
    void OnDisable() => GameSession.OnBalanceChanged -= Refresh;

    void Refresh() => gameObject.SetActive(GameSession.Owns(upgradeId));
}