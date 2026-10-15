using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;
namespace ActionTimer;
internal sealed class AppearanceWindow : Window
{
    private readonly Plugin plugin;
    private bool testStarted;
    private double testStartTime;
    internal AppearanceWindow(Plugin plugin) : base("Action Timer 共通設定###ActionTimerAppearance")
    {
        this.plugin = plugin;
        Size = new Vector2(560, 650);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    public override void Draw()
    {
        var appearance = plugin.Config.Appearance;
        ImGui.TextWrapped("すべてのジョブ・アイコンに適用されます。変更は自動保存されます。");
        ImGui.TextUnformatted("数字フォント：Miedinger Mid");
        var changed = DrawTextSettings("リキャスト秒数", appearance.Recast);
        changed |= DrawTextSettings("チャージ数", appearance.Charges);
        ImGui.Separator();
        ImGui.TextUnformatted("アイコンの縁取り線");
        changed |= ImGui.Checkbox("枠線を表示", ref appearance.BorderEnabled);
        changed |= ImGui.ColorEdit4("枠線の色", ref appearance.BorderColor, ImGuiColorEditFlags.NoInputs);
        changed |= ImGui.SliderFloat("枠線の太さ", ref appearance.BorderWidth, 1, 6, "%.0f px");
        ImGui.Separator();
        var sweep = (int)appearance.Sweep;
        ImGui.SetNextItemWidth(240);
        if (ImGui.Combo("グラデーション", ref sweep, "時計回り\0上から\0なし\0"))
        {
            appearance.Sweep = (SweepMode)sweep;
            changed = true;
        }
        ImGui.TextWrapped("チャージが1以上残っている場合、グラデーションは表示しません。");
        if (changed) plugin.Save();
        ImGui.Separator();
        ImGui.TextUnformatted("プレビュー・20秒テストラン");
        ImGui.TextWrapped("通常の20秒リキャストを再現します。ゲーム内アクションは実行しません。");
        ImGui.SetNextItemWidth(240);
        if (ImGui.SliderFloat("プレビューアイコンサイズ", ref plugin.Config.PreviewIconSize, 16, 256, "%.0f px"))
        {
            plugin.Config.PreviewIconSize = float.IsFinite(plugin.Config.PreviewIconSize)
                ? Math.Clamp(plugin.Config.PreviewIconSize, 16, 256) : 128;
            plugin.Save();
        }
        ImGui.TextWrapped("プレビュー専用です。実際に配置したアイコンのサイズは変更しません。");
        if (ImGui.Button(testStarted ? "20秒テストを再開始" : "20秒テスト開始"))
        {
            testStarted = true;
            testStartTime = ImGui.GetTime();
        }
        ImGui.SameLine();
        if (ImGui.Button("リセット")) testStarted = false;
        var remaining = testStarted ? PreviewTimer.Remaining(ImGui.GetTime(), testStartTime) : 0;
        ImGui.TextUnformatted(!testStarted ? "待機中" : remaining > 0 ? $"テスト中：残り {remaining:0.0} 秒" : "完了：使用可能");
        var action = plugin.PreviewAction();
        if (action.HasValue) ImGui.TextUnformatted($"プレビューアイコン：{action.Value.Name}");
        var at = ImGui.GetCursorScreenPos() + new Vector2(8);
        var draw = ImGui.GetWindowDrawList();
        var size = float.IsFinite(plugin.Config.PreviewIconSize)
            ? Math.Clamp(plugin.Config.PreviewIconSize, 16, 256) : 128;
        draw.PushClipRect(at, at + new Vector2(size), true);
        var texture = action.HasValue ? Plugin.Textures.GetFromGameIcon((uint)action.Value.Icon).GetWrapOrDefault() : null;
        if (texture != null) draw.AddImage(texture.Handle, at, at + new Vector2(size));
        else draw.AddRectFilled(at, at + new Vector2(size), 0xFF705030);
        if (remaining > 0)
            Plugin.DrawSweep(draw, at, size, CooldownVisual.Progress(remaining, PreviewTimer.Duration), appearance.Sweep);
        var text = testStarted
            ? remaining > 0 ? (remaining < 10 ? remaining.ToString("0.0") : Math.Ceiling(remaining).ToString("0")) : ""
            : "12.3";
        if (text.Length > 0)
        {
            var extent = plugin.MeasureLabel(text, appearance.Recast.Size);
            plugin.DrawLabel(draw, at + (new Vector2(size) - extent) / 2, text, appearance.Recast);
        }
        if (!testStarted) plugin.DrawLabel(draw, at + new Vector2(3, 1), "2", appearance.Charges);
        Plugin.DrawBorder(draw, at, size, appearance);
        draw.PopClipRect();
        ImGui.Dummy(new Vector2(size + 16));
    }
    private static bool DrawTextSettings(string label, TextAppearance style)
    {
        ImGui.PushID(label);
        ImGui.Separator();
        ImGui.TextUnformatted(label);
        var changed = ImGui.SliderFloat("文字サイズ", ref style.Size, 8, 96, "%.0f px");
        changed |= ImGui.ColorEdit4("文字色", ref style.Color, ImGuiColorEditFlags.NoInputs);
        changed |= ImGui.Checkbox("縁取り文字", ref style.OutlineEnabled);
        changed |= ImGui.ColorEdit4("縁取り色", ref style.OutlineColor, ImGuiColorEditFlags.NoInputs);
        changed |= ImGui.SliderFloat("縁取りの太さ", ref style.OutlineWidth, 1, 4, "%.0f px");
        ImGui.PopID();
        return changed;
    }
}




