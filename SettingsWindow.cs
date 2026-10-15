using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;
namespace ActionTimer;
internal sealed class SettingsWindow : Window
{
    private readonly Plugin plugin;
    private uint selectedJob;
    internal uint SelectedJob => selectedJob;
    private string search = "";
    internal SettingsWindow(Plugin plugin) : base("Action Timer 設定###ActionTimerSettings")
    {
        this.plugin = plugin;
        selectedJob = plugin.Jobs.FirstOrDefault().RowId;
        Size = new Vector2(820, 570);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    public override void Draw()
    {
        if (ImGui.Button("共通設定を開く")) plugin.OpenAppearance();
        ImGui.Checkbox("配置編集（現在のジョブのアイコンをドラッグ）", ref plugin.Edit);
        ImGui.TextWrapped("座標はゲーム画面左上からのピクセル値です。サイズは16～256px。設定は自動保存されます。");
        ImGui.TextWrapped("対象：PvEアビリティと固有リキャストのウェポンスキル。数字は残り秒数、チャージ式は次の補充までの秒数と保有数です。");
        if (ImGui.BeginListBox("ジョブ", new Vector2(200, -1)))
        {
            foreach (var job in plugin.Jobs)
                if (ImGui.Selectable(job.Name.ToString(), selectedJob == job.RowId)) selectedJob = job.RowId;
            ImGui.EndListBox();
        }
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(280);
        ImGui.InputText("検索", ref search, 100);
        if (ImGui.BeginChild("abilities", new Vector2(0, 0)))
        {
            foreach (var action in plugin.Actions(selectedJob))
            {
                if (!action.Name.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                ImGui.PushID((int)action.RowId);
                // Do not create stored configuration until a user enables an action.
                var enabled = plugin.Config.Jobs.TryGetValue(selectedJob, out var items) && items.TryGetValue(action.RowId, out var existing) && existing.Enabled;
                if (ImGui.Checkbox($"{action.Name}  (Lv.{action.ClassJobLevel})", ref enabled))
                {
                    plugin.Config.Get(selectedJob, action.RowId).Enabled = enabled;
                    plugin.Save();
                }
                if (enabled)
                {
                    var icon = plugin.Config.Get(selectedJob, action.RowId);
                    ImGui.SetNextItemWidth(105);
                    var changed = ImGui.InputFloat("X", ref icon.X, 1, 10, "%.0f");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(105);
                    changed |= ImGui.InputFloat("Y", ref icon.Y, 1, 10, "%.0f");
                    ImGui.SetNextItemWidth(240);
                    changed |= ImGui.SliderFloat("サイズ", ref icon.Size, 16, 256, "%.0f px");
                    ImGui.Separator();
                    if (changed)
                    {
                        icon.X = float.IsFinite(icon.X) ? Math.Max(0, icon.X) : 0;
                        icon.Y = float.IsFinite(icon.Y) ? Math.Max(0, icon.Y) : 0;
                        icon.Size = float.IsFinite(icon.Size) ? Math.Clamp(icon.Size, 16, 256) : 48;
                        plugin.Save();
                    }
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.EndGroup();
    }
}




