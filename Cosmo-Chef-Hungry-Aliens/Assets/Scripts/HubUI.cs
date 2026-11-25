using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] Button payButton;
    [SerializeField] TMP_Text payButtonText;
    [SerializeField] Button callBackButton;

    [Header("Дзвінок: діалоги")]
    [SerializeField] ParentDialogueDirector parents;
    [SerializeField] Button choiceAButton;
    [SerializeField] TMP_Text choiceAText;
    [SerializeField] Button choiceBButton;
    [SerializeField] TMP_Text choiceBText;

    [Header("Результат")]
    [SerializeField] TMP_Text resultText;
    [SerializeField] Button continueButton;

    [Header("Посилання")]
    [SerializeField] OrderManager orders;

    [Header("Фідбек")]
    [SerializeField] float panelFadeTime = 0.15f;
    [SerializeField] float resultCountTime = 0.9f;

    bool _hintUsed = false;
    int _currentMarketTab = 0;

    readonly Dictionary<GameObject, Coroutine> _fades = new Dictionary<GameObject, Coroutine>();
    Coroutine _resultAnim;

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

        // Звук кліку на всіх кнопках сцени
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            b.onClick.AddListener(PlayClick);

        HideAll();
    }

    void PlayClick() => Feedback.Sfx("click", 0.7f);

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

    // ---------- М'які переходи панелей ----------

    void Show(GameObject panel)
    {
        panel.SetActive(true);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        Coroutine running;
        if (_fades.TryGetValue(panel, out running) && running != null) StopCoroutine(running);

        cg.alpha = 0f;
        _fades[panel] = StartCoroutine(FadeIn(cg));
    }

    IEnumerator FadeIn(CanvasGroup cg)
    {
        for (float t = 0f; t < panelFadeTime; t += Time.unscaledDeltaTime)
        {
            cg.alpha = t / panelFadeTime;
            yield return null;
        }
        cg.alpha = 1f;
    }

    public void OpenLaptop()
    {
        _hintUsed = true;
        HideAll();
        Show(laptopMenu);
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
        Show(laptopMenu);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshWallet();
    }

    void OpenMarket()
    {
        laptopMenu.SetActive(false);
        Show(marketPanel);
        ShowMarketCategory(0);
    }

    void OpenCall()
    {
        laptopMenu.SetActive(false);
        Show(callPanel);
        ShowNode(parents.BeginCall());
        RefreshCall();
    }

    void StartShift(int days)
    {
        ShiftSettings.Days = days;
        HideAll();
        GameFlow.Set(GamePhase.Shift);
        orders.StartShift();
    }

    // ---------- Результат зміни ----------

    public void ShowResult(int served, int lost, int credits)
    {
        HideAll();
        Show(resultPanel);
        GameFlow.Set(GamePhase.Result);

        Feedback.Sfx("end");

        if (_resultAnim != null) StopCoroutine(_resultAnim);
        _resultAnim = StartCoroutine(AnimateResult(served, lost));
    }

    // Цифри «накручуються», потім звук залежить від того, чи зміна була прибутковою
    IEnumerator AnimateResult(int served, int lost)
    {
        for (float t = 0f; t < resultCountTime; t += Time.unscaledDeltaTime)
        {
            float u = t / resultCountTime;
            float k = 1f - Mathf.Pow(1f - u, 3f);
            resultText.text = BuildResult(served, lost, k);
            yield return null;
        }

        resultText.text = BuildResult(served, lost, 1f);

        int net = GameSession.ShiftRevenue - GameSession.ShiftExpenses;
        Feedback.Sfx(net >= 0 ? "coin" : "error");
        _resultAnim = null;
    }

    string BuildResult(int served, int lost, float k)
    {
        int total = served + lost;
        int percent = total > 0 ? Mathf.RoundToInt(100f * served / total) : 0;

        int revenue = GameSession.ShiftRevenue;
        int expenses = GameSession.ShiftExpenses;
        int interest = GameSession.ShiftInterest;
        int net = revenue - expenses;

        int shownServed = Mathf.RoundToInt(served * k);
        int shownLost = Mathf.RoundToInt(lost * k);
        int shownRevenue = Mathf.RoundToInt(revenue * k);
        int shownExpenses = Mathf.RoundToInt(expenses * k);
        int shownNet = Mathf.RoundToInt(Mathf.Abs(net) * k);

        string netColor = net >= 0 ? "#7CFF8A" : "#FF7A6B";
        string netSign = net >= 0 ? "+" : "−";

        string text =
            "Зміну завершено\n\n" +
            $"Обслуговано: {shownServed}\n" +
            $"Втрачено: {shownLost}\n" +
            $"Ефективність: {percent}%\n\n" +
            $"Виручка: {shownRevenue}\n" +
            $"Витрати на закупівлі: −{shownExpenses}\n";

        if (interest > 0)
            text += $"Відсотки (додано до боргу): +{interest}\n";

        text += $"<color={netColor}>Чистий результат: {netSign}{shownNet}</color>\n\n" +
                $"Кредити: {GameSession.Credits}    Борг: {GameSession.Debt}";

        return text;
    }

    void ContinueAfterResult()
    {
        HideAll();

        ChefInteraction chef = FindFirstObjectByType<ChefInteraction>();
        if (chef != null) chef.ResetHand();

        GameFlow.Set(GamePhase.Hub);
    }

    void RefreshWallet()
    {
        string text = $"Кредити: {GameSession.Credits}\nБорг: {GameSession.Debt}";
        if (walletText != null) walletText.text = text;
        if (marketWalletText != null) marketWalletText.text = $"Кредити: {GameSession.Credits}";
    }

    // ---------- Маркет ----------

    void ShowMarketCategory(int index, bool keepScroll = false)
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

        ScrollRect scroll = rowsParent != null ? rowsParent.GetComponentInParent<ScrollRect>() : null;
        float scrollPos = scroll != null ? scroll.verticalNormalizedPosition : 1f;

        // Вимикаємо одразу, а знищуємо в кінці кадру: інакше старі рядки ще мить псують розкладку
        foreach (Transform child in rowsParent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

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

        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = keepScroll ? scrollPos : 1f;
        }

        RefreshWallet();
    }

    void BuyUpgrade(Upgrade u)
    {
        if (GameSession.TryBuy(u))
        {
            Debug.Log($"[Market] куплено: {u.title}, залишилось кредитів: {GameSession.Credits}");
            Feedback.Sfx("buy");
            Feedback.Good($"Куплено: {u.title}");
        }
        else
        {
            int missing = u.price - GameSession.Credits;
            Feedback.Warn(missing > 0 ? $"Не вистачає {missing} кредитів" : "Поки недоступно");
        }

        // Перемальовуємо поточну вкладку, не скидаючи прокрутку
        ShowMarketCategory(_currentMarketTab, true);
    }

    // ---------- Дзвінок ----------

    void RefreshCall()
    {
        int payable = Mathf.Min(GameSession.Debt, GameSession.Credits);
        payButtonText.text = GameSession.Debt <= 0 ? "Борг сплачено" : $"Сплатити {payable}";
        payButton.interactable = payable > 0;
        RefreshChoices();
    }

    void PayDebt()
    {
        int paid = GameSession.PayDebt(GameSession.Debt);
        Debug.Log($"[Call] сплачено {paid}, лишилось боргу: {GameSession.Debt}");

        if (paid > 0)
        {
            Feedback.Sfx("coin");
            Feedback.Good($"Сплачено {paid}. Борг: {GameSession.Debt}");
            ShowNode(parents.AfterPayment(paid));
        }

        RefreshCall();
    }

    void ShowNode(DialogueNode n) => callText.text = n != null ? n.Formatted : "";

    void RefreshChoices()
    {
        var list = parents.GetChoices();
        BindChoice(choiceAButton, choiceAText, list.Count > 0 ? list[0] : null);
        BindChoice(choiceBButton, choiceBText, list.Count > 1 ? list[1] : null);
    }

    void BindChoice(Button button, TMP_Text label, DialogueChoice choice)
    {
        button.onClick.RemoveAllListeners();
        button.gameObject.SetActive(choice != null);
        if (choice == null) return;

        label.text = choice.label;
        button.onClick.AddListener(PlayClick);
        button.onClick.AddListener(() =>
        {
            ShowNode(choice.Pick());
            RefreshCall();   // оновлює і кнопку боргу (гроші змінились), і набір виборів
        });
    }

    void HideAll()
    {
        laptopMenu.SetActive(false);
        marketPanel.SetActive(false);
        callPanel.SetActive(false);
        resultPanel.SetActive(false);
    }
}