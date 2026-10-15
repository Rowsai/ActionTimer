using System.Numerics;
namespace ActionTimer;

public enum SweepMode { Clockwise, TopToBottom, None }

internal static class CooldownVisual
{
    public static bool ShouldShade(int maxCharges, uint charges) => maxCharges <= 1 || charges == 0;

    public static float Progress(float remaining, float duration) =>
        duration > 0 && float.IsFinite(duration) && float.IsFinite(remaining)
            ? Math.Clamp(1 - remaining / duration, 0, 1) : 1;

    // A soft leading edge reveals the original icon, ending fully clear.
    public static float Shade(float location, float progress)
    {
        if (progress <= 0) return 0.65f;
        if (progress >= 1) return 0;
        var width = Math.Min(0.08f, Math.Min(progress, 1 - progress));
        return 0.65f * Math.Clamp((location - progress) / width + 0.5f, 0, 1);
    }

    public static Vector2 SquareEdge(float turn)
    {
        var angle = turn * MathF.Tau - MathF.PI / 2;
        var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        return ray / Math.Max(Math.Abs(ray.X), Math.Abs(ray.Y));
    }
}

