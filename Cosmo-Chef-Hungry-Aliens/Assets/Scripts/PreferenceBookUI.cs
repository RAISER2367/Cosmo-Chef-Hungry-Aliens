using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PreferenceBookUI : MonoBehaviour
{
    public static PreferenceBookUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._isOpen;
    public static bool ConsumedEscape => IsOpen || (Instance != null && Instance._closedFrame == Time.frameCount);

    [Header("Панель")]
    [SerializeField] GameObject panel;
    [SerializeField] Button closeButton;

    [Header("Вкладки видів (у тому ж порядку, що й PreferenceBook.Species)")]
    [SerializeField] Button[] speciesTabButtons;
    [SerializeField] Image[] speciesTabHighlights;
    [SerializeField] Color activeTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] Color inactiveTabColor = Color.white;

    [Header("Рядки вподобань")]
    [SerializeField] PreferenceRow rowPrefab;
    [SerializeField] Transform rowsParent;

    [Header("Поведінка під час зміни")]
    [Tooltip("1 = без уповільнення, менше 1 = трохи сповільнює час, поки відкрито")]
    [Range(0.1f, 1f)][SerializeField] float shiftOpenTimeScale = 1f;

    [Header("Переклад станів")]
    [SerializeField] string rawLabel = "сире";
    [SerializeField] string cutLabel = "нарізане";
    [SerializeField] string readyLabel = "готове";
    [SerializeField] string burntLabel = "підгоріле";

    [Header("Переклад інгредієнтів")]
    [SerializeField] string meatLabel = "м'ясо";
    [SerializeField] string cucumberLabel = "огірок";

    bool _isOpen;
    int _currentTab;
    int _closedFrame = -1;
    float _previousTimeScale = 1f;

    void Awake()
    {
        Instance = this;
        closeButton.onClick.AddListener(Close);

        for (int i = 0; i < speciesTabButtons.Length; i++)
        {
            int idx = i;
            speciesTabButtons[i].onClick.AddListener(() => ShowSpecies(idx));
        }

        panel.SetActive(false);
    }

    void Update()
    {
        if (_isOpen)
        {
            int count = PreferenceBook.Species.Length;

            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_currentTab > 0) ShowSpecies(_currentTab - 1);
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_currentTab < count - 1) ShowSpecies(_currentTab + 1);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
        }

        if (GameFlow.Phase != GamePhase.Shift) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_isOpen) Close();
            else Open();
        }
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        panel.SetActive(true);

        // Кнопка закриття видима лише в ноутбуці, під час зміни ховаємо
        if (closeButton != null)
            closeButton.gameObject.SetActive(GameFlow.Phase != GamePhase.Shift);

        ShowSpecies(0);
        SyncCursor();

        if (GameFlow.Phase == GamePhase.Shift && shiftOpenTimeScale < 1f)
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = shiftOpenTimeScale;
        }
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _closedFrame = Time.frameCount;

        panel.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        SyncCursor();

        if (!Mathf.Approximately(Time.timeScale, _previousTimeScale))
            Time.timeScale = _previousTimeScale;
    }

    void SyncCursor()
    {
        bool locked = GameFlow.CanMove;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void ShowSpecies(int index)
    {
        _currentTab = index;
        SpeciesDefinition species = PreferenceBook.Species[index];

        for (int i = 0; i < speciesTabButtons.Length; i++)
        {
            if (speciesTabButtons[i] == null) continue;

            ColorBlock colors = speciesTabButtons[i].colors;
            colors.normalColor = i == index ? activeTabColor : inactiveTabColor;
            colors.selectedColor = colors.normalColor;
            speciesTabButtons[i].colors = colors;
        }

        for (int i = 0; i < speciesTabHighlights.Length; i++)
            if (speciesTabHighlights[i] != null)
                speciesTabHighlights[i].enabled = i == index;

        foreach (Transform child in rowsParent) Destroy(child.gameObject);

        foreach (var (food, state, level) in PreferenceBook.GetAll(species.id))
        {
            PreferenceRow row = Instantiate(rowPrefab, rowsParent);
            row.Setup(IngredientLabel(food), StateLabel(state), level);
        }
    }

    string StateLabel(FoodState state) => state switch
    {
        FoodState.Raw => rawLabel,
        FoodState.Cut => cutLabel,
        FoodState.Ready => readyLabel,
        FoodState.Burnt => burntLabel,
        _ => state.ToString()
    };

    string IngredientLabel(string food) => food switch
    {
        "meat" => meatLabel,
        "cucumber" => cucumberLabel,
        _ => food
    };
}