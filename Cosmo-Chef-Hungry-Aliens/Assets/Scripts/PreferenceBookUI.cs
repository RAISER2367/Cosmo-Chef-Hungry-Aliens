using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PreferenceBookUI : MonoBehaviour
{
    public static PreferenceBookUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._isOpen;

    [Header("Панель")]
    [SerializeField] GameObject panel;
    [SerializeField] Button closeButton;

    [Header("Вкладки видів (у тому ж порядку, що й PreferenceBook.Species)")]
    [SerializeField] Button[] speciesTabButtons;
    [SerializeField] Image[] speciesTabHighlights;

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
        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
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
        ShowSpecies(0);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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

        panel.SetActive(false);

        // Скидаємо фокус з кнопки, щоб вона не лишалася білою при наступному відкритті
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // Повертаємо курсор у стан, який відповідає поточній фазі (Shift -> прихований, Laptop -> видимий)
        bool locked = GameFlow.CanMove;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;

        if (!Mathf.Approximately(Time.timeScale, _previousTimeScale))
            Time.timeScale = _previousTimeScale;
    }

    void ShowSpecies(int index)
    {
        SpeciesDefinition species = PreferenceBook.Species[index];

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