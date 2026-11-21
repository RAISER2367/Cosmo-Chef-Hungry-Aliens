using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] Button notebookButton;

    [Header("Маркетплейс")]
    [SerializeField] Button[] categoryTabButtons; // Порядок: Kitchen, Logistics, Finance, Cosmetic
    [SerializeField] Color activeTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] Color inactiveTabColor = Color.white;
    [SerializeField] TMP_Text marketWalletText;   // Опційно: баланс всередині самого маркету
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
    int _currentMarketTab = 0;

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
        notebookButton.onClick.AddListener(() => PreferenceBookUI.Instance.Open());

        for (int i = 0; i < categoryTabButtons.Length; i++)
        {
            int idx = i;
            categoryTabButtons[i].onClick.AddListener(() => ShowMarketCategory(idx));
        }

        HideAll();
    }

    void Start()
    {
        GameFlow.Set(GamePhase.Hub);
    }

    void Update()
    {
        hubHint.SetActive(GameFlow.Phase == GamePhase.Hub && !_hintUsed);

        if (PreferenceBookUI.ConsumedEscape) return;

        if (GameFlow.Phase == GamePhase.Laptop)
        {
            // Перемикання категорій маркету клавішами A / D без зациклення
            if (marketPanel.activeSelf && categoryTabButtons.Length > 0)
            {
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    if (_currentMarketTab > 0) ShowMarketCategory(_currentMarketTab - 1);
                }
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (_currentMarketTab < categoryTabButtons.Length - 1) ShowMarketCategory(_currentMarketTab + 1);
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (marketPanel.activeSelf || callPanel.activeSelf) BackToMenu();
                else CloseLaptop();
            }
        }
    }

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

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshWallet();
    }

    void OpenMarket()
    {
        laptopMenu.SetActive(false);
        marketPanel.SetActive(true);
        ShowMarketCategory(0);
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
            $"Ефективність: {percent}%\n" +
            $"Заробіток: {credits} кредитів";

        resultPanel.SetActive(true);
        GameFlow.Set(GamePhase.Result);
    }

    void ContinueAfterResult()
    {
        HideAll();

        ChefInteraction chef = FindFirstObjectByType<ChefInteraction>();
        if (chef != null) chef.Hand = null;

        GameFlow.Set(GamePhase.Hub);
    }

    void RefreshWallet()
    {
        string text = $"Кредити: {GameSession.Credits}\nБорг: {GameSession.Debt}";
        if (walletText != null) walletText.text = text;
        if (marketWalletText != null) marketWalletText.text = $"Кредити: {GameSession.Credits}";
    }

    void ShowMarketCategory(int index)
    {
        _currentMarketTab = index;

        for (int i = 0; i < categoryTabButtons.Length; i++)
        {
            if (categoryTabButtons[i] == null) continue;

            ColorBlock colors = categoryTabButtons[i].colors;
            colors.normalColor = i == index ? activeTabColor : inactiveTabColor;
            colors.selectedColor = colors.normalColor;
            categoryTabButtons[i].colors = colors;
        }

        foreach (Transform child in rowsParent) Destroy(child.gameObject);

        if (rowPrefab == null)
        {
            Debug.LogError("[Market] Row Prefab не призначено в HubUI");
            return;
        }

        UpgradeCategory category = (UpgradeCategory)index;
        foreach (Upgrade u in UpgradeCatalog.InCategory(category))
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

        // Перемальовуємо поточну вкладку, щоб не перекидало на першу
        ShowMarketCategory(_currentMarketTab);
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