using Dalamud.Configuration;
namespace ActionTimer;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public float PreviewIconSize = 128;
    public AppearanceSettings Appearance { get; set; } = new();
    public Dictionary<uint, Dictionary<uint, IconSettings>> Jobs { get; set; } = new();
    public IconSettings Get(uint job, uint action)
    {
        if (!Jobs.TryGetValue(job, out var actions)) Jobs[job] = actions = new();
        if (!actions.TryGetValue(action, out var setting))
            actions[action] = setting = new IconSettings { X = 100 + actions.Count % 10 * 60, Y = 100 + actions.Count / 10 * 60 };
        return setting;
    }
}
[Serializable]
public sealed class IconSettings
{
    public bool Enabled;
    public float X = 100;
    public float Y = 100;
    public float Size = 48;

}


[Serializable]
public sealed class AppearanceSettings
{
    public SweepMode Sweep = SweepMode.TopToBottom;
    public TextAppearance Recast = new() { Size = 20 };
    public TextAppearance Charges = new() { Size = 16, Color = new(1, 1, 0, 1) };
    public bool BorderEnabled = true;
    public System.Numerics.Vector4 BorderColor = new(1, 1, 0, 1);
    public float BorderWidth = 1;
}
[Serializable]
public sealed class TextAppearance
{
    public float Size = 20;
    public System.Numerics.Vector4 Color = System.Numerics.Vector4.One;
    public bool OutlineEnabled = true;
    public System.Numerics.Vector4 OutlineColor = new(0, 0, 0, 1);
    public float OutlineWidth = 1;
}



