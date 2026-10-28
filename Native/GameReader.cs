using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using KeyboundTab.Diagnostics;

namespace KeyboundTab.Native;

/// <summary>Bounded reads of KTGInput / KTGEWord only. No native addresses escape a scan.</summary>
internal sealed unsafe class GameReader
{
    public static InputReport CaptureInput(out ushort addonId)
    {
        addonId = 0;
        var manager = RaptureAtkUnitManager.Instance();
        if (!ReadableMemory.Contains(manager, (nuint)sizeof(RaptureAtkUnitManager)))
            return ReadInput(null);
        var addon = manager->GetAddonByName("KTGInput");
        if (!ReadableMemory.Contains(addon, (nuint)sizeof(AtkUnitBase)) || addon->NumBlockingAddons != 0)
            return ReadInput(null);
        addonId = addon->Id;
        return ReadInput(addon);
    }

    public ProbeReport Capture(bool compact = true)
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (!ReadableMemory.Contains(manager, (nuint)sizeof(RaptureAtkUnitManager)))
            return Empty("界面管理器不可读");
        var list = &manager->AllLoadedUnitsList;
        if (list->Count > list->Entries.Length)
            return Empty("界面实例数量异常");

        var addons = new List<AddonReport>();
        AtkUnitBase* inputAddon = null;
        for (var i = 0; i < list->Count; i++)
        {
            var addon = list->Entries[i].Value;
            if (!ReadableMemory.Contains(addon, (nuint)sizeof(AtkUnitBase)))
                continue;
            var name = addon->NameString;
            if (name is not ("KTGInput" or "KTGEWord" or "KTGEnemyCastBar" or "KTGIGaugeHp" or "KTGHudTodo"))
                continue;
            if (addons.Count >= 64)
                return Empty("小游戏界面实例过多");
            var nodes = name == "KTGInput" && compact ? [] : this.ReadNodes(&addon->UldManager, 0, addon->IsVisible, compact);
            var moduleBase = System.Diagnostics.Process.GetCurrentProcess().MainModule!.BaseAddress;
            addons.Add(new AddonReport(name, addon->Id, addon->IsVisible, addon->IsReady, nodes, *(nint*)addon - moduleBase));
            if (name == "KTGInput" && addon->IsVisible)
                inputAddon = addon;
        }

        var input = ReadInput(inputAddon);
        return new ProbeReport(addons.Count == 0 ? "未发现魔光键影界面" : "只读采集", input, addons.ToArray(), addons.Count == 0 ? null : ReadActivity(),
            addons.Any(a => a.Name == "KTGEWord" && a.Visible) ? EnemyReader.Capture() : []);
    }

    internal static InputReport ReadInput(AtkUnitBase* addon)
    {
        var input = FindInput(addon);
        if (input == null)
            return new InputReport(false, false, false, false, "", "");
        return new InputReport(true, input->Enabled, input->IsActive, IsFocused(input), ReadString(&input->RawString), ReadString(&input->EvaluatedString),
            !input->Enabled || addon->NumBlockingAddons != 0
            || !ReadableMemory.Contains(input->OwnerNode, (nuint)sizeof(AtkComponentNode))
            || (input->OwnerNode->NodeFlags & NodeFlags.Enabled) == 0, input->ComponentFlags);
    }

    public static ActivityReport? ReadActivity()
    {
        var timer = InputTimerModule.Instance();
        if (!ReadableMemory.Contains(timer, (nuint)sizeof(InputTimerModule))
            || !float.IsFinite(timer->InputTimer) || !float.IsFinite(timer->ContentInputTimer)
            || timer->InputTimer is < -1 or > 604800 || timer->ContentInputTimer is < -1 or > 604800)
            return null;
        return new ActivityReport(timer->InputTimer, timer->ContentInputTimer, timer->AfkTimer, timer->InstanceContentAfkTimeLimit, timer->Status);
    }

    internal static AtkComponentTextInput* FindInput(AtkUnitBase* addon)
    {
        if (!ReadableMemory.Contains(addon, (nuint)sizeof(AtkUnitBase)) || addon->NameString != "KTGInput" || !addon->IsVisible)
            return null;
        var node = FindNode(&addon->UldManager, 7);
        if (!ReadableMemory.Contains(node, (nuint)sizeof(AtkComponentNode)) || (ushort)node->Type < 1000)
            return null;
        var component = ((AtkComponentNode*)node)->Component;
        if (!ReadableMemory.Contains(component, (nuint)sizeof(AtkComponentTextInput)))
            return null;
        var info = component->UldManager.Objects;
        if (!ReadableMemory.Contains(info, (nuint)sizeof(AtkUldComponentInfo))
            || ((AtkUldComponentInfo*)info)->ComponentType != ComponentType.TextInput)
            return null;
        return (AtkComponentTextInput*)component;
    }

    internal static bool IsFocused(AtkComponentTextInput* input)
    {
        var module = RaptureAtkModule.Instance();
        if (!ReadableMemory.Contains(module, (nuint)sizeof(RaptureAtkModule)))
            return false;
        // Compare interface identity; no virtual calls or parent/sibling traversal.
        return module->TextInput.TargetTextInputEventInterface == &input->AtkTextInputEventInterface;
    }

    internal static AtkResNode* FindNode(AtkUldManager* manager, uint id)
    {
        if (!ValidNodes(manager))
            return null;
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (ReadableMemory.Contains(node, (nuint)sizeof(AtkResNode)) && node->NodeId == id)
                return node;
        }
        return null;
    }

    private NodeReport[] ReadNodes(AtkUldManager* manager, int depth, bool inheritedVisible, bool compact)
    {
        if (depth > 2 || !ValidNodes(manager))
            return [];
        var result = new List<NodeReport>();
        var knownNodes = new HashSet<nint>();
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var entry = manager->NodeList[i];
            if (ReadableMemory.Contains(entry, (nuint)sizeof(AtkResNode)))
                knownNodes.Add((nint)entry);
        }
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (!ReadableMemory.Contains(node, (nuint)sizeof(AtkResNode)))
                continue;
            string? text = null;
            NodeReport[]? children = null;
            var visible = inheritedVisible && EffectiveVisible(node, knownNodes);
            if (compact && !visible)
                continue;
            if (node->Type == NodeType.Text && ReadableMemory.Contains(node, (nuint)sizeof(AtkTextNode)))
                text = ReadString(&((AtkTextNode*)node)->NodeText);
            else if ((ushort)node->Type >= 1000 && ReadableMemory.Contains(node, (nuint)sizeof(AtkComponentNode)))
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (ReadableMemory.Contains(component, (nuint)sizeof(AtkComponentBase)))
                    children = this.ReadNodes(&component->UldManager, depth + 1, visible, compact);
            }
            if (!compact || text is { Length: > 0 } || children is { Length: > 0 })
                result.Add(new NodeReport(node->NodeId, node->Type.ToString(), visible, text, children, node->ScreenX, node->ScreenY));
        }
        return result.ToArray();
    }

    private static bool EffectiveVisible(AtkResNode* node, HashSet<nint> knownNodes)
    {
        // Only inspect parents already validated in this bounded node array.
        // Never follow a parent outside it or walk renderer/sibling/child chains.
        for (var depth = 0; depth < 32; depth++)
        {
            if ((node->NodeFlags & NodeFlags.Visible) == 0)
                return false;
            var parent = node->ParentNode;
            if (parent == null || !knownNodes.Contains((nint)parent))
                return true;
            node = parent;
        }
        return false;
    }

    private static bool ValidNodes(AtkUldManager* manager) =>
        ReadableMemory.Contains(manager, (nuint)sizeof(AtkUldManager))
        && manager->NodeListCount is > 0 and <= 128
        && manager->NodeListCount <= manager->NodeListSize
        && ReadableMemory.Contains(manager->NodeList, (nuint)(manager->NodeListCount * sizeof(nint)));

    internal static string ReadString(Utf8String* value)
    {
        if (!ReadableMemory.Contains(value, (nuint)sizeof(Utf8String)) || value->BufUsed is < 1 or > 4097
            || value->BufSize < value->BufUsed || !ReadableMemory.Contains(value->StringPtr, (nuint)value->BufUsed))
            return "";
        return SeString.Parse(value->AsSpan()).TextValue;
    }

    private static ProbeReport Empty(string note) => new(note, new InputReport(false, false, false, false, "", ""), []);
}
