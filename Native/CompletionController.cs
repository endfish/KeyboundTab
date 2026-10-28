using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KeyboundTab.Core;
using KeyboundTab.Diagnostics;
using Lumina.Excel.Sheets;

namespace KeyboundTab.Native;

internal sealed unsafe class CompletionController : IDisposable
{
    private readonly Configuration configuration;
    private readonly string gameVersion;
    private readonly IPluginLog log;
    private readonly WordCatalog catalog;
    private readonly GameReader reader = new();
    private readonly CompletionEngine engine = new();
    private readonly PressGate press = new();
    private bool failed;
    private bool settingsOpen;

    public string Status { get; private set; } = "等待进入小游戏并聚焦输入框";

    public CompletionController(Configuration configuration, string gameVersion, IDataManager data, IPluginLog log)
    {
        this.configuration = configuration;
        this.gameVersion = gameVersion;
        this.log = log;
        try
        {
            var sheet = data.GetExcelSheet<KTGTypeWordTextData>();
            this.catalog = new WordCatalog(sheet.Select(row => new WordCandidate(row.Unknown0.ExtractText(), row.Unknown1.ExtractText())));
        }
        catch (Exception ex)
        {
            this.catalog = new WordCatalog([]);
            this.Fail(ex, "游戏词库读取失败");
        }
        if (!RuntimeCompatibility.CanWrite(gameVersion))
        {
            this.Status = "当前游戏版本尚未适配";
            return;
        }
        try
        {
            var moduleBase = System.Diagnostics.Process.GetCurrentProcess().MainModule!.BaseAddress;
            if ((nint)AtkTextInput.MemberFunctionPointers.ProcessKeyShortcut != moduleBase + 0x687E70
                || (nint)AtkComponentTextInput.MemberFunctionPointers.InsertText != moduleBase + 0x6B5D80)
                throw new InvalidOperationException("Native input functions differ from the inspected game executable.");
        }
        catch (Exception ex)
        {
            this.Fail(ex, "原生输入接口校验失败");
        }
    }

    private ShortcutBinding Binding => new(this.configuration.Key, this.configuration.Control, this.configuration.Alt, this.configuration.Shift);

    public void Tick(bool settingsOpen)
    {
        this.settingsOpen = settingsOpen;
        if (this.failed || !RuntimeCompatibility.CanWrite(this.gameVersion))
            return;
        if (!this.configuration.Enabled)
        {
            this.Status = "补全已关闭";
            this.Reset();
            return;
        }
        try
        {
            var binding = this.Binding;
            var down = WindowsInput.IsDown(binding.Key);
            var eligible = this.IsEligible(binding, out var input, out var addonId);
            var pressed = this.press.Update(down, eligible);
            if (!eligible)
            {
                this.engine.Reset();
                return;
            }
            this.ObserveInput(input, addonId);
            if (pressed)
                this.TryFill(addonId);
            // Observe physical presses only; do not hook, suppress or clear input.
        }
        catch (Exception ex)
        {
            this.Fail(ex, "补全发生错误，已停止");
        }
    }

    private bool IsEligible(ShortcutBinding binding, out InputReport input, out ushort addonId)
    {
        input = new InputReport(false, false, false, false, "", "");
        addonId = 0;
        if (!binding.IsValid)
            return false;
        if (this.settingsOpen || ImGui.GetIO().WantCaptureKeyboard)
        {
            this.Status = "设置或插件输入界面正在使用键盘";
            return false;
        }
        if (!WindowsInput.IsGameForeground(out var window) || !WindowsInput.Matches(binding))
            return false;
        input = GameReader.CaptureInput(out addonId);
        if (input.Blocked || !input.Enabled || !input.Active || !input.Focused)
        {
            this.Status = "等待小游戏输入框获得焦点";
            return false;
        }
        if (WindowsInput.IsComposing(window) || input.Raw != input.Evaluated)
        {
            this.Status = "输入法组词中，先完成或取消组词";
            return false;
        }
        return true;
    }

    private void ObserveInput(InputReport input, ushort addonId)
    {
        var context = $"KTGInput:{addonId}";
        this.engine.Observe(input.Raw, context);
    }

    private void TryFill(ushort addonId)
    {
        var snapshot = this.reader.Capture();
        var addon = snapshot.Addons.SingleOrDefault(a => a.Name == "KTGInput" && a.Visible && a.Id == addonId);
        if (addon == null || snapshot.Input.Blocked || !snapshot.Input.Focused || snapshot.Input.Raw != snapshot.Input.Evaluated)
            return;
        var context = $"KTGInput:{addonId}";
        var candidates = CandidateReader.Read(snapshot, this.catalog, this.configuration.PrioritizeCasting);
        var selected = this.engine.Select(snapshot.Input.Raw, candidates, context, Random.Shared);
        if (selected == null)
        {
            this.Status = "当前没有可识别的词条，保留原输入";
            this.log.Information("[KeyboundTab] Shortcut: no visible recognized words.");
            return;
        }
        if (!GameInputWriter.TryReplace(this.gameVersion, addon.Id, snapshot.Input.Raw, selected.Input, out var reason))
        {
            this.Status = "输入框暂时不可写或定型文菜单已打开，未发送任何文字";
            this.log.Warning("[KeyboundTab] Shortcut: native input rejected; candidates={Count}; reason={Reason}.", candidates.Length, reason);
            return;
        }
        this.engine.ConfirmWrite(selected.Input, context);
        this.Status = $"已填入：{selected.Display}（由你按 Enter 发送）";
        this.log.Information("[KeyboundTab] Filled {Word}; candidates={Count}; castingPriority={Priority}; no submit; activity={Activity}.", selected.Display, candidates.Length, selected.Priority, GameReader.ReadActivity()?.ToString() ?? "unavailable");
    }

    private void Fail(Exception ex, string reason)
    {
        this.failed = true;
        this.configuration.Enabled = false;
        this.Reset();
        this.Status = reason;
        this.log.Error(ex, "[KeyboundTab] {Reason}", reason);
    }

    public void Reset()
    {
        this.engine.Reset();
        this.press.Reset();
    }

    public void Dispose()
    {
        this.Reset();
    }
}
