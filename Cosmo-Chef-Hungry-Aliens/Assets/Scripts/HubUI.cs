using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HubUI : MonoBehaviour
{
    public static HubUI Instance { get; private set; }

    [Header("Панелі")]
    [SerializeField] GameObject laptopMenu;
    [SerializeField] GameObject marketPanel;
    [SerializeField] GameObject callPanel;
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject hubHint;

    [Header("Меню ноутбука")]
    [SerializeField] TMP_Text walletText;
    [SerializeField] Button marketButton;
    [SerializeField] Button callButton;
    [SerializeField] Button sprintButton;
    [SerializeField] Button standardButton;
    [SerializeField] Button marathonButton;
    [SerializeField] Button closeButton;

    [Header("Маркетплейс")]
    [SerializeField] UpgradeRow rowPrefab;
    [SerializeField] Transform rowsParent;
    [SerializeField] Button marketBackButton;

    [Header("Дзвінок")]
    [SerializeField] TMP_Text callText;
    [SerializeField] string[] dialogue = { "Синку, коли вже віддаси борг?" };
    [SerializeField] Button payButton;
    [SerializeField] TMP_Text payButtonText;
    [SerializeField] Button callBackButton;

    [Header("Результат")]
    [SerializeField] TMP_Text resultText;
    [SerializeField] Button continueButton;

    [Header("Посилання")]
    [SerializeField] OrderManager orders;

    bool _hintUsed = false;

    void Awake()
    {
        Instance = this;
        GameSession.ResetAll();

        marketButton.onClick.AddListener(OpenMarket);
        callButton.onClick.AddListener(OpenCall);
        closeButton.onClick.AddListener(CloseLaptop);
        marketBackButton.onClick.AddListener(BackToMenu);
        callBackButton.onClick.AddListener(BackToMenu);
        payButton.onClick.AddListener(PayDebt);
        sprintButton.onClick.AddListener(() => StartShift(1));
        standardButton.onClick.AddListener(() => StartShift(3));
        marathonButton.onClick.AddListener(() => StartShift(7));
        continueButton.onClick.AddListener(ContinueAfterResult);

        HideAll();
    }

    void Start()
    {
        GameFlow.Set(GamePhase.Hub);
    }

    void Update()
    {
        hubHint.SetActive(GameFlow.Phase == GamePhase.Hub && !_hintUsed);

        if (GameFlow.Phase == GamePhase.Laptop && Input.GetKeyDown(KeyCode.Escape))
        {
            if (marketPanel.activeSelf || callPanel.activeSelf) BackToMenu();
            else CloseLaptop();
        }
    }

    // Викликає Laptop, коли гравець натискає E на ноутбуку
    public void OpenLaptop()
    {
        _hintUsed = true;
        HideAll();
        laptopMenu.SetActive(true);
        GameFlow.Set(GamePhase.Laptop);
        RefreshWallet();
    }

    void CloseLaptop()
    {
        HideAll();
        GameFlow.Set(GamePhase.Hub);
    }

    void BackToMenu()
    {
        marketPanel.SetActive(false);
        callPanel.SetActive(false);
        laptopMenu.SetActive(true);
        RefreshWallet();
    }

    void OpenMarket()
    {
        laptopMenu.SetActive(false);
        marketPanel.SetActive(true);
        RefreshMarket();
    }

    void OpenCall()
    {
        laptopMenu.SetActive(false);
        callPanel.SetActive(true);
        callText.text = dialogue[UnityEngine.Random.Range(0, dialogue.Length)];
        RefreshCall();
    }

    void StartShift(int days)
    {
        ShiftSettings.Days = days;
        HideAll();
        GameFlow.Set(GamePhase.Shift);
        orders.StartShift();
    }

    public void ShowResult(int served, int lost, int credits)
    {
        HideAll();

        int total = served + lost;
        int percent = total > 0 ? Mathf.RoundToInt(100f * served / total) : 0;

        resultText.text =
            "Зміну завершено\n\n" +
            $"Обслуговано: {served}\n" +
            $"Втрачено: {lost}\n" +
            $"Ефективність: {percent}%" +
            $"Заробіток: {credits} кредитів";

        resultPanel.SetActive(true);
        GameFlow.Set(GamePhase.Result);
    }

    void ContinueAfterResult()
    {
        HideAll();

        // Прибираємо страву з рук, щоб у хабі нічого не лишалось
        ChefInteraction chef = FindFirstObjectByType<ChefInteraction>();
        if (chef != null) chef.Hand = null;

        GameFlow.Set(GamePhase.Hub);
    }

    void RefreshWallet()
    {
        walletText.text = $"Кредити: {GameSession.Credits}\nБорг: {GameSession.Debt}";
    }

    void RefreshMarket()
    {
        foreach (Transform child in rowsParent) Destroy(child.gameObject);

        if (rowPrefab == null)
        {
            Debug.LogError("[Market] Row Prefab не призначено в HubUI");
            return;
        }

        foreach (Upgrade u in UpgradeCatalog.All)
        {
            UpgradeRow row = Instantiate(rowPrefab, rowsParent);
            row.Setup(u, () => BuyUpgrade(u));
        }

        RefreshWallet();
    }

    void BuyUpgrade(Upgrade u)
    {
        if (GameSession.TryBuy(u))
            Debug.Log($"[Market] куплено: {u.title}, залишилось кредитів: {GameSession.Credits}");

        RefreshMarket();
    }

    void RefreshCall()
    {
        int payable = Mathf.Min(GameSession.Debt, GameSession.Credits);
        payButtonText.text = GameSession.Debt <= 0 ? "Борг сплачено" : $"Сплатити {payable}";
        payButton.interactable = payable > 0;
    }

    void PayDebt()
    {
        int paid = GameSession.PayDebt(GameSession.Debt);
        Debug.Log($"[Call] сплачено {paid}, лишилось боргу: {GameSession.Debt}");
        RefreshCall();
    }

    void HideAll()
    {
        laptopMenu.SetActive(false);
        marketPanel.SetActive(false);
        callPanel.SetActive(false);
        resultPanel.SetActive(false);
    }
}