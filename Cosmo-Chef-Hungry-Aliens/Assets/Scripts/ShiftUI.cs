using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShiftUI : MonoBehaviour
{
    public static ShiftUI Instance { get; private set; }

    [Header("Стартова панель")]
    [SerializeField] GameObject startPanel;
    [SerializeField] Button sprintButton;
    [SerializeField] Button standardButton;
    [SerializeField] Button marathonButton;

    [Header("Панель результату")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] TMP_Text resultText;
    [SerializeField] Button restartButton;

    [Header("Посилання")]
    [SerializeField] OrderManager orders;

    void Awake()
    {
        Instance = this;

        sprintButton.onClick.AddListener(() => StartWith(1));
        standardButton.onClick.AddListener(() => StartWith(3));
        marathonButton.onClick.AddListener(() => StartWith(7));
        restartButton.onClick.AddListener(Restart);

        startPanel.SetActive(true);
        resultPanel.SetActive(false);
    }

    // Start, а не Awake: щоб після Start контролера гравця не перебити курсор
    IEnumerator Start()
    {
        yield return null;
        ShowMenu();
    }

    void ShowMenu()
    {
        Time.timeScale = 0f;
        SetCursor(true);
    }

    void StartWith(int days)
    {
        ShiftSettings.Days = days;
        startPanel.SetActive(false);
        StartCoroutine(BeginNextFrame());
    }

    IEnumerator BeginNextFrame()
    {
        // Клік по кнопці не має одразу потрапити у взаємодію Chef
        yield return null;

        Time.timeScale = 1f;
        SetCursor(false);
        orders.StartShift();
        Debug.Log($"[Shift] старт: {ShiftSettings.Days} д., {ShiftSettings.Duration} с");
    }

    public void ShowResult(int served, int lost, int credits)
    {
        int total = served + lost;
        int percent = total > 0 ? Mathf.RoundToInt(100f * served / total) : 0;

        resultText.text =
            "Зміну завершено\n\n" +
            $"Обслуговано: {served}\n" +
            $"Втрачено: {lost}\n" +
            $"Ефективність: {percent}%\n" +
            $"Кредитів: {credits}";

        resultPanel.SetActive(true);
        ShowMenu();
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    static void SetCursor(bool visible)
    {
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }
}