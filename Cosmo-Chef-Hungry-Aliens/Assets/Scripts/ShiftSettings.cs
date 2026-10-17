public static class ShiftSettings
{
    public const float SecondsPerDay = 180f; // 1 ігровий день = 3 хв, як у GDD

    public static int Days = 1;
    public static float Duration => Days * SecondsPerDay;
}