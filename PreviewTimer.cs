namespace ActionTimer;
internal static class PreviewTimer
{
    public const float Duration = 20;
    public static float Remaining(double now, double start) =>
        (float)Math.Clamp(Duration - (now - start), 0, Duration);
}
