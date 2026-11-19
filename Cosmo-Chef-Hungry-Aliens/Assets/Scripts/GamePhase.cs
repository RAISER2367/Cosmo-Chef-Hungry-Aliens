using UnityEngine;

public enum GamePhase { Hub, Shift, Laptop, Result }

public static class GameFlow
{
    public static GamePhase Phase { get; private set; } = GamePhase.Hub;

    // Гравець може рухатися та взаємодіяти лише коли він у Хабі або на Зміні, і блокнот закритий
    public static bool CanMove => (Phase == GamePhase.Hub || Phase == GamePhase.Shift) && !PreferenceBookUI.IsOpen;
    public static bool CanInteract => CanMove;

    public static void Set(GamePhase phase)
    {
        Phase = phase;

        bool locked = CanMove;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}