using UnityEngine;

public enum GamePhase { Hub, Shift, Laptop, Result }

public static class GameFlow
{
    public static GamePhase Phase { get; private set; } = GamePhase.Hub;

    // Час останньої зміни фази: клік по кнопці UI не має одразу перетворитись на дію в світі
    public static float LastChangeTime { get; private set; } = -10f;
    public static bool JustChanged => Time.unscaledTime - LastChangeTime < 0.15f;

    // Гравець може рухатися та взаємодіяти лише коли він у Хабі або на Зміні, і блокнот закритий
    public static bool CanMove => (Phase == GamePhase.Hub || Phase == GamePhase.Shift) && !PreferenceBookUI.IsOpen;
    public static bool CanInteract => CanMove;

    public static void Set(GamePhase phase)
    {
        Phase = phase;
        LastChangeTime = Time.unscaledTime;

        bool locked = CanMove;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}