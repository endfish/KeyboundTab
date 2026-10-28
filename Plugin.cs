using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KeyboundTab.Core;
using KeyboundTab.Diagnostics;
using KeyboundTab.Native;

namespace KeyboundTab;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;

    private readonly Configuration configuration;
    private readonly GameReader reader = new();
    private readonly ProbeRecorder recorder;
    private readonly string gameVersion = RuntimeCompatibility.ReadGameVersion();
    private readonly CompletionController controller;
    private bool settingsOpen;
    private bool recording = true;
    private bool scanFailed;
    private long nextScan;
    private ProbeReport? latest;

    public Plugin()
    {
        this.configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (this.configuration.Version < 2)
        {
            this.configuration.Version = 2;
            this.configuration.Enabled = true;
        }
        if (!this.Binding.IsValid)
        {
            this.configuration.Key = 0x09;
            this.configuration.Control = this.configuration.Alt = this.configuration.Shift = false;
        }
        if (!this.CanWrite)
            this.configuration.Enabled = false;
        this.recorder = new ProbeRecorder(PluginInterface.GetPluginConfigDirectory());
        this.controller = new CompletionController(this.configuration, this.gameVersion, Data, Log);
        Commands.AddHandler("/kbt", new CommandInfo(this.OnCommand)
        {
            HelpMessage = "魔光键影补全设置；on/off 启停，debug 开关本地采集，dump 采集当前界面。",
        });
        PluginInterface.UiBuilder.Draw += this.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenSettings;
        Framework.Update += this.Update;
        Log.Information("[KeyboundTab] Loaded; game={GameVersion}, supported={Supported}, diagnosticFile={Path}",
            this.gameVersion, this.CanWrite, this.recorder.PathName);
        Chat.Print(this.CanWrite ? $"魔光键影补全已加载：{this.Binding} 填词，Enter 自己发送。/kbt 设置。"
            : "当前游戏版本尚未适配，补全已关闭。/kbt 查看状态。", "KeyboundTab");
    }

    private bool CanWrite => RuntimeCompatibility.CanWrite(this.gameVersion);
    private ShortcutBinding Binding => new(this.configuration.Key, this.configuration.Control, this.configuration.Alt, this.configuration.Shift);

    public void Dispose()
    {
        Framework.Update -= this.Update;
        PluginInterface.UiBuilder.Draw -= this.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenSettings;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenSettings;
        Commands.RemoveHandler("/kbt");
        this.controller.Dispose();
        this.recorder.Dispose();
    }

    private void OpenSettings() => this.settingsOpen = true;

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on":
                if (!this.CanWrite)
                    Chat.Print("当前游戏版本尚未适配，补全未开启。", "KeyboundTab");
                else
                {
                    this.configuration.Enabled = true;
                    this.Save();
                    Chat.Print($"已启用：{this.Binding} 填词，Enter 手动发送。", "KeyboundTab");
                }
                break;
            case "off":
                this.configuration.Enabled = false;
                this.Save();
                Chat.Print("补全已关闭。", "KeyboundTab");
                break;
            case "debug":
                this.recording = !this.recording;
                Chat.Print(this.recording ? "本地只读采集已开启。" : "本地采集已停止。", "KeyboundTab");
                break;
            case "dump":
                this.Capture(true);
                Chat.Print(this.scanFailed ? "界面采集失败，已停止自动采集，请查看日志。" : "已采集当前小游戏界面。", "KeyboundTab");
                break;
            case "status":
                Chat.Print(this.controller.Status, "KeyboundTab");
                break;
            default:
                this.OpenSettings();
                break;
        }
    }

    private void Update(IFramework framework)
    {
        this.controller.Tick(this.settingsOpen);
        if (!this.recording || this.scanFailed || Environment.TickCount64 < this.nextScan)
            return;
        this.nextScan = Environment.TickCount64 + 200;
        this.Capture(false);
    }

    private void Capture(bool force)
    {
        try
        {
            this.latest = this.reader.Capture(compact: !force);
            this.recorder.Record(this.latest, force);
        }
        catch (Exception ex)
        {
            this.scanFailed = true;
            this.configuration.Enabled = false;
            this.controller.Reset();
            Log.Error(ex, "[KeyboundTab] Probe stopped after an error.");
        }
    }

    private void Draw()
    {
        if (!this.settingsOpen)
            return;
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 340), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("魔光键影补全设置###KeyboundTab", ref this.settingsOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("快捷键只填词，Enter 由你自己发送。");
            ImGui.BeginDisabled(!this.CanWrite);
            var enabled = this.configuration.Enabled;
            if (ImGui.Checkbox("启用补全", ref enabled))
            {
                this.configuration.Enabled = enabled;
                this.Save();
            }
            ImGui.EndDisabled();
            if (!this.CanWrite)
                ImGui.TextWrapped("当前游戏版本尚未适配，补全已关闭。");
            ImGui.TextWrapped(this.controller.Status);

            var currentKey = this.configuration.Key;
            var index = Array.FindIndex(ShortcutBinding.Choices, c => c.Key == currentKey);
            var names = ShortcutBinding.Choices.Select(c => c.Name).ToArray();
            if (ImGui.Combo("补全按键", ref index, names, names.Length))
            {
                this.configuration.Key = ShortcutBinding.Choices[index].Key;
                this.ValidateBinding();
                this.Save();
            }
            var ctrl = this.configuration.Control;
            var alt = this.configuration.Alt;
            var shift = this.configuration.Shift;
            var changed = ImGui.Checkbox("Ctrl", ref ctrl);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("Alt", ref alt);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("Shift", ref shift);
            if (changed)
            {
                this.configuration.Control = ctrl;
                this.configuration.Alt = alt;
                this.configuration.Shift = shift;
                this.ValidateBinding();
                this.Save();
            }
            ImGui.TextUnformatted($"当前快捷键：{this.Binding}");
            ImGui.TextWrapped("仅在小游戏输入框获得焦点时响应快捷键；长按只触发一次。");
            var prioritize = this.configuration.PrioritizeCasting;
            if (ImGui.Checkbox("读条词条增加抽中概率（保留其他词条）", ref prioritize))
            {
                this.configuration.PrioritizeCasting = prioritize;
                this.Save();
            }
            ImGui.TextWrapped("连续按快捷键可换选其他词条；填好后由你回车发送。");
            ImGui.Separator();
            ImGui.Checkbox("采集小游戏界面（仅保存在本机）", ref this.recording);
            if (ImGui.Button("采集当前界面"))
                this.Capture(true);
            ImGui.TextUnformatted(this.latest?.Note ?? "等待采集");
            ImGui.TextUnformatted($"词条界面：{this.latest?.Addons.Count(a => a.Name == "KTGEWord" && a.Visible) ?? 0}；输入框焦点：{this.latest?.Input.Focused ?? false}");
        }
        ImGui.End();
    }

    private void ValidateBinding()
    {
        if (this.Binding.IsValid)
            return;
        this.configuration.Alt = false;
        Chat.Print("Alt+Tab 和 Alt+F4 保留给系统，已取消 Alt 修饰键。", "KeyboundTab");
    }

    private void Save()
    {
        PluginInterface.SavePluginConfig(this.configuration);
        this.controller.Reset();
    }
}
