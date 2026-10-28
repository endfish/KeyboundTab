using System.Runtime.InteropServices;
using KeyboundTab.Core;

namespace KeyboundTab.Native;

internal static class WindowsInput
{
    public static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    public static bool IsGameForeground(out nint window)
    {
        window = GetForegroundWindow();
        return window != 0 && GetWindowThreadProcessId(window, out var process) != 0
            && process == Environment.ProcessId;
    }

    public static bool Matches(ShortcutBinding binding) => binding.MatchesModifiers(
        IsDown(0x11), IsDown(0x12), IsDown(0x10), IsDown(0x5B) || IsDown(0x5C));

    public static bool IsComposing(nint window)
    {
        var context = ImmGetContext(window);
        if (context == 0)
            return false;
        try
        {
            // A negative result cannot establish that composition has ended.
            return ImmGetCompositionStringW(context, 8, 0, 0) != 0;
        }
        finally
        {
            ImmReleaseContext(window, context);
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("imm32.dll", ExactSpelling = true)] private static extern nint ImmGetContext(nint window);
    [DllImport("imm32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ImmReleaseContext(nint window, nint context);
    [DllImport("imm32.dll", ExactSpelling = true)] private static extern int ImmGetCompositionStringW(nint context, uint index, nint buffer, uint length);
}
