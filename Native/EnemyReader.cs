using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KeyboundTab.Diagnostics;

namespace KeyboundTab.Native;

/// <summary>Read-only view of the shared KTG word/cast data; verified against this client's native UI update routines.</summary>
internal static unsafe class EnemyReader
{
    public static EnemyReport[] Capture()
    {
        var stage = AtkStage.Instance();
        if (!ReadableMemory.Contains(stage, (nuint)sizeof(AtkStage)))
            return [];
        var holder = stage->AtkArrayDataHolder;
        if (!ReadableMemory.Contains(holder, (nuint)sizeof(AtkArrayDataHolder))
            || holder->NumberArrayCount is <= 161 or > 1024 || holder->StringArrayCount is <= 128 or > 1024
            || !ReadableMemory.Contains(holder->NumberArrays, (nuint)(holder->NumberArrayCount * sizeof(nint)))
            || !ReadableMemory.Contains(holder->StringArrays, (nuint)(holder->StringArrayCount * sizeof(nint))))
            return [];
        // AddonKTGEWord::OnRequestedUpdate and AddonKTGEnemyCastBar::OnRequestedUpdate
        // read number array 161, string array 128: count + 13 ints / 3 strings per target.
        var numbers = holder->NumberArrays[161];
        var strings = holder->StringArrays[128];
        if (!ReadableMemory.Contains(numbers, (nuint)sizeof(NumberArrayData))
            || !ReadableMemory.Contains(strings, (nuint)sizeof(StringArrayData))
            || numbers->Size is < 1 or > 1024 || strings->Size is < 1 or > 1024
            || !ReadableMemory.Contains(numbers->IntArray, (nuint)(numbers->Size * sizeof(int)))
            || !ReadableMemory.Contains(strings->StringArray, (nuint)(strings->Size * sizeof(nint))))
            return [];
        var count = numbers->IntArray[0];
        if (count is < 0 or > 20 || numbers->Size < 1 + count * 13 || strings->Size < count * 3)
            return [];
        var result = new List<EnemyReport>();
        for (var i = 0; i < count; i++)
        {
            var data = new ReadOnlySpan<int>(numbers->IntArray + 1 + i * 13, 13).ToArray();
            var id = unchecked((uint)data[0]);
            if (id is 0 or 0xE0000000)
                continue;
            result.Add(new EnemyReport(id, ReadText(strings->StringArray[i * 3]),
                ReadText(strings->StringArray[i * 3 + 1]), ReadText(strings->StringArray[i * 3 + 2]), data));
        }
        return result.ToArray();
    }

    private static string ReadText(byte* pointer)
    {
        if (pointer == null)
            return "";
        for (var length = 0; length < 512; length++)
        {
            if (length % 64 == 0 && !ReadableMemory.Contains(pointer + length, 64))
                return "";
            if (pointer[length] == 0)
                return SeString.Parse(new ReadOnlySpan<byte>(pointer, length)).TextValue;
        }
        return "";
    }
}
