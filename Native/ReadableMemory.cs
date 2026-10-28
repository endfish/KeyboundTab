using System.Runtime.InteropServices;

namespace KeyboundTab.Native;

internal static unsafe class ReadableMemory
{
    public static bool Contains(void* address, nuint size)
    {
        var current = (nuint)address;
        if (current < 0x10000 || size == 0 || size > 0x1000000 || current > nuint.MaxValue - size)
            return false;
        var end = current + size;
        while (current < end)
        {
            if (VirtualQuery((nint)current, out var region, (nuint)sizeof(MemoryBasicInformation)) == 0
                || region.State != 0x1000 || (region.Protect & 0x100) != 0
                || (region.Protect & 0xFF) is not (0x02 or 0x04 or 0x08 or 0x20 or 0x40 or 0x80))
                return false;
            var next = (nuint)region.BaseAddress + region.RegionSize;
            if (next <= current)
                return false;
            current = next;
        }
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nuint VirtualQuery(nint address, out MemoryBasicInformation information, nuint length);
}
