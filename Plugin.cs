using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Lumina.Excel.Sheets;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using GameAction = Lumina.Excel.Sheets.Action;

namespace ActionTimer;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    internal readonly Configuration Config;
    private readonly IFontHandle numericFont;
    private readonly WindowSystem windows = new("ActionTimer");
    private readonly SettingsWindow settings;
    private readonly AppearanceWindow appearanceWindow;
    internal readonly List<ClassJob> Jobs;
    private readonly Dictionary<uint, List<GameAction>> actions = new();
    internal bool Edit;

    public Plugin()
    {
        Config = Pi.GetPluginConfig() as Configuration ?? new();
        Jobs = Data.GetExcelSheet<ClassJob>(ClientLanguage.Japanese)!
            .Where(j => j.JobIndex > 0 && j.Name.ToString().Length > 0).ToList();
        numericFont = Pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.MiedingerMid, 36));
        settings = new SettingsWindow(this);
        windows.AddWindow(settings);
        appearanceWindow = new AppearanceWindow(this);
        windows.AddWindow(appearanceWindow);
        Commands.AddHandler("/actiontimer", new CommandInfo(OnCommand) { HelpMessage = "Action Timer 設定。/actiontimer edit で配置編集を切替。" });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenConfigUi += Open;
        Pi.UiBuilder.OpenMainUi += Open;
    }
    internal List<GameAction> Actions(uint job)
    {
        if (actions.TryGetValue(job, out var list)) return list;
        var abbreviation = Jobs.First(j => j.RowId == job).Abbreviation.ToString();
        var property = typeof(ClassJobCategory).GetProperty(abbreviation);
        list = Data.GetExcelSheet<GameAction>(ClientLanguage.Japanese)!
            .Where(a => a.IsPlayerAction && !a.IsPvP && (a.ActionCategory.RowId == 4 ||
                (a.ActionCategory.RowId == 3 &&
                 ((a.CooldownGroup > 0 && a.CooldownGroup != 58) || (a.AdditionalCooldownGroup > 0 && a.AdditionalCooldownGroup != 58)))) &&
                a.Icon > 0 && a.Name.ToString().Length > 0 &&
                property?.GetValue(a.ClassJobCategory.Value) is true)
            .OrderBy(a => a.ClassJobLevel).ThenBy(a => a.RowId).ToList();
        actions[job] = list;
        return list;
    }
    internal GameAction? PreviewAction()
    {
        var candidates = Actions(settings.SelectedJob);
        if (Config.Jobs.TryGetValue(settings.SelectedJob, out var selected))
            foreach (var action in candidates)
                if (selected.TryGetValue(action.RowId, out var icon) && icon.Enabled) return action;
        return candidates.Count > 0 ? candidates[0] : null;
    }

    internal void OpenAppearance() => appearanceWindow.IsOpen = true;
    internal void Save() => Pi.SavePluginConfig(Config);
    private void Open() => settings.IsOpen = true;
    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("edit", StringComparison.OrdinalIgnoreCase)) Edit = !Edit;
        else if (args.Trim().Equals("style", StringComparison.OrdinalIgnoreCase)) OpenAppearance();
        else settings.Toggle();
    }
    private void Draw()
    {
        windows.Draw();
        if (!Client.IsLoggedIn || !Player.IsLoaded) return;
        var job = Player.ClassJob.RowId;
        if (!Config.Jobs.TryGetValue(job, out var selected)) return;
        foreach (var action in Actions(job))
            if (selected.TryGetValue(action.RowId, out var icon) && icon.Enabled)
                DrawIcon(job, action, icon);
    }
    private unsafe void DrawIcon(uint job, GameAction action, IconSettings icon)
    {
        var manager = ActionManager.Instance();
        if (manager == null) return;
        // Follow upgraded / transformed actions as the game's hotbar does.
        var id = manager->GetAdjustedActionId(action.RowId);
        var adjusted = Data.GetExcelSheet<GameAction>(ClientLanguage.Japanese)!.GetRowOrDefault(id) ?? action;
        var total = manager->GetRecastTime(ActionType.Action, id);
        var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, id);
        var remaining = manager->IsRecastTimerActive(ActionType.Action, id) ? Math.Max(0, total - elapsed) : 0;
        var maxCharges = Math.Max(1, (int)ActionManager.GetMaxCharges(id, 0));
        var charges = manager->GetCurrentCharges(id);
        // Charge gauge elapsed includes credit for unused charges. Show time to next charge.
        if (maxCharges > 1 && remaining > 0 && total > 0)
        {
            var interval = total / maxCharges;
            remaining = interval - elapsed % interval;
        }
        var duration = maxCharges > 1 ? total / maxCharges : total;
        var progress = CooldownVisual.Progress(remaining, duration);
        var viewport = ImGui.GetMainViewport();
        var size = Math.Clamp(icon.Size, 16, 256);
        var position = viewport.Pos + new Vector2(icon.X, icon.Y);
        position = Vector2.Clamp(position, viewport.Pos, viewport.Pos + Vector2.Max(Vector2.Zero, viewport.Size - new Vector2(size)));
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(size), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing;
        if (!Edit) flags |= ImGuiWindowFlags.NoInputs;
        if (ImGui.Begin($"##ActionTimer-{job}-{action.RowId}", flags))
        {
            var draw = ImGui.GetWindowDrawList();
            var texture = Textures.GetFromGameIcon((uint)adjusted.Icon).GetWrapOrDefault();
            if (texture != null) draw.AddImage(texture.Handle, position, position + new Vector2(size));
            else draw.AddRectFilled(position, position + new Vector2(size), 0xFF555555);
            if (remaining > 0)
            {
                if (CooldownVisual.ShouldShade(maxCharges, charges))
                    DrawSweep(draw, position, size, progress, Config.Appearance.Sweep);
                var text = remaining < 10 ? remaining.ToString("0.0") : Math.Ceiling(remaining).ToString("0");
                var fontSize = Math.Clamp(Config.Appearance.Recast.Size, 8, 96);
                var extent = MeasureLabel(text, fontSize);
                DrawLabel(draw, position + (new Vector2(size) - extent) / 2, text, Config.Appearance.Recast);
            }
            if (maxCharges > 1)
                DrawLabel(draw, position + new Vector2(3, 1), charges.ToString(),
                    Config.Appearance.Charges);
            DrawBorder(draw, position, size, Config.Appearance);
            if (Edit)
            {
                // Border appearance is shared in both normal and edit mode.
                ImGui.InvisibleButton("drag", new Vector2(size));
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var next = position - viewport.Pos + ImGui.GetIO().MouseDelta;
                    icon.X = Math.Clamp(next.X, 0, Math.Max(0, viewport.Size.X - size));
                    icon.Y = Math.Clamp(next.Y, 0, Math.Max(0, viewport.Size.Y - size));
                }
                if (ImGui.IsItemDeactivated()) Save();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{action.Name}\nドラッグで移動");
            }
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
    }
    internal Vector2 MeasureLabel(string text, float size)
    {
        using var font = numericFont.Push();
        return ImGui.CalcTextSize(text) * (size / ImGui.GetFontSize());
    }

    internal void DrawLabel(ImDrawListPtr draw, Vector2 at, string text, TextAppearance style)
    {
        using var font = numericFont.Push();
        var size = Math.Clamp(style.Size, 8, 96);
        if (style.OutlineEnabled)
        {
            var width = Math.Clamp(style.OutlineWidth, 1, 4);
            var color = ImGui.ColorConvertFloat4ToU32(style.OutlineColor);
            // Render around the entire glyph, including inner rings for thick outlines.
            for (var radius = 1; radius <= (int)Math.Ceiling(width); radius++)
                for (var i = 0; i < 16; i++)
                {
                    var angle = MathF.Tau * i / 16;
                    var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Math.Min(radius, width);
                    draw.AddText(ImGui.GetFont(), size, at + offset, color, text);
                }
        }
        draw.AddText(ImGui.GetFont(), size, at, ImGui.ColorConvertFloat4ToU32(style.Color), text);
    }

    internal static void DrawBorder(ImDrawListPtr draw, Vector2 at, float size, AppearanceSettings style)
    {
        if (!style.BorderEnabled) return;
        var width = Math.Clamp(style.BorderWidth, 1, 6);
        var color = ImGui.ColorConvertFloat4ToU32(style.BorderColor);
        // Keep the complete border inside the icon's clipping rectangle.
        draw.AddRectFilled(at, at + new Vector2(size, width), color);
        draw.AddRectFilled(at + new Vector2(0, size - width), at + new Vector2(size), color);
        draw.AddRectFilled(at + new Vector2(0, width), at + new Vector2(width, size - width), color);
        draw.AddRectFilled(at + new Vector2(size - width, width), at + new Vector2(size, size - width), color);
    }
    internal static void DrawSweep(ImDrawListPtr draw, Vector2 at, float size, float progress, SweepMode mode)
    {
        if (mode == SweepMode.None || progress >= 1) return;
        const int segments = 128;
        if (mode == SweepMode.TopToBottom)
        {
            for (var i = 0; i < segments; i++)
            {
                var alpha = CooldownVisual.Shade((i + 0.5f) / segments, progress);
                if (alpha <= 0) continue;
                var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, alpha));
                draw.AddRectFilled(at + new Vector2(0, size * i / segments),
                    at + new Vector2(size, size * (i + 1) / segments), color);
            }
        }
        else
        {
            var center = at + new Vector2(size / 2);
            // Segments meet all four corners; each triangle stays inside the square.
            var previousFlags = draw.Flags;
            draw.Flags &= ~ImDrawListFlags.AntiAliasedFill;
            for (var i = 0; i < segments; i++)
            {
                var alpha = CooldownVisual.Shade((i + 0.5f) / segments, progress);
                if (alpha <= 0) continue;
                var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, alpha));
                draw.AddTriangleFilled(center,
                    center + CooldownVisual.SquareEdge((float)i / segments) * (size / 2),
                    center + CooldownVisual.SquareEdge((float)(i + 1) / segments) * (size / 2), color);
            }
            draw.Flags = previousFlags;
        }
    }
    public void Dispose()
    {
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Pi.UiBuilder.OpenMainUi -= Open;
        Commands.RemoveHandler("/actiontimer");
        windows.RemoveAllWindows();
        numericFont.Dispose();
        Save();
    }
}







