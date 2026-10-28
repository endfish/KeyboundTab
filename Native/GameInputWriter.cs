using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KeyboundTab.Core;

namespace KeyboundTab.Native;

internal static unsafe class GameInputWriter
{
    public static bool TryReplace(string gameVersion, ushort addonId, string expected, string replacement, out string reason)
    {
        reason = "unsupported version or empty replacement";
        if (!RuntimeCompatibility.CanWrite(gameVersion) || replacement.Length == 0 || replacement.Contains('\0'))
            return false;
        var manager = RaptureAtkUnitManager.Instance();
        reason = "unreadable manager";
        if (!ReadableMemory.Contains(manager, (nuint)sizeof(RaptureAtkUnitManager)))
            return false;
        var list = &manager->AllLoadedUnitsList;
        reason = "invalid addon count";
        if (list->Count > list->Entries.Length)
            return false;
        for (var i = 0; i < list->Count; i++)
        {
            var addon = list->Entries[i].Value;
            if (!ReadableMemory.Contains(addon, (nuint)sizeof(AtkUnitBase)) || addon->Id != addonId)
                continue;
            var input = GameReader.FindInput(addon);
            reason = "input focus, blocking addon, or changed original text";
            if (input == null || GameReader.ReadInput(addon).Blocked || !input->Enabled || !input->IsActive || !GameReader.IsFocused(input)
                || GameReader.ReadString(&input->RawString) != expected || addon->NumBlockingAddons != 0)
                return false;
            var limits = &input->ComponentTextData;
            reason = $"input limits: MaxByte={limits->MaxByte}, MaxChar={limits->MaxChar}, bytes={Encoding.UTF8.GetByteCount(replacement)}, chars={replacement.EnumerateRunes().Count()}";
            if (!InputLimits.Fits(Encoding.UTF8.GetByteCount(replacement), replacement.EnumerateRunes().Count(), limits->MaxByte, limits->MaxChar)
                || AtkComponentTextInput.MemberFunctionPointers.InsertText == null
                || AtkTextInput.MemberFunctionPointers.ProcessKeyShortcut == null)
                return false;

            var module = RaptureAtkModule.Instance();
            reason = "unreadable text input module";
            if (!ReadableMemory.Contains(module, (nuint)sizeof(RaptureAtkModule)))
                return false;
            var textInput = &module->TextInput;
            reason = $"completionDepth={textInput->CompletionDepth}, evaluatedMatches={GameReader.ReadString(&input->EvaluatedString) == expected}";
            if (textInput->CompletionDepth != 0 || GameReader.ReadString(&input->EvaluatedString) != expected)
                return false;

            // Use the native select-all / insert route so the focused editor buffer,
            // selection and cursor stay synchronized. No Enter or submit callbacks.
            var modifiers = new AtkTextInput.KeyModifiers { IsControlDown = true };
            reason = "native Ctrl+A returned false";
            if (!textInput->ProcessKeyShortcut(SeVirtualKey.A, &modifiers))
                return false;
            input->InsertText(replacement);
            reason = $"readback: raw={GameReader.ReadString(&input->RawString)}, evaluated={GameReader.ReadString(&input->EvaluatedString)}";
            return GameReader.ReadString(&input->RawString) == replacement
                && GameReader.ReadString(&input->EvaluatedString) == replacement;
        }
        return false;
    }
}
