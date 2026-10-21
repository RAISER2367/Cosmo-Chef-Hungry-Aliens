using UnityEngine;

public enum GamePhase { Hub, Shift, Laptop, Result }

// Єдине місце, де вирішується, що зараз можна робити
public static class GameFlow
{
    public static GamePhase Phase { get; private set; } = GamePhase.Hub;

    public static bool CanMove => Phase == GamePhase.Hub || Phase == GamePhase.Shift;
    public static bool CanInteract => CanMove;

    public static void Set(GamePhase phase)
    {
        Phase = phase;

        // Курсор захоплений, поки гравець ходить; в меню він вільний
        bool locked = CanMove;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}