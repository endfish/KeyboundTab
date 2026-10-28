namespace KeyboundTab.Core;

public readonly record struct ShortcutBinding(int Key, bool Control, bool Alt, bool Shift)
{
    public static readonly (int Key, string Name)[] Choices =
    [
        (0x09, "Tab"), (0x20, "Space"),
        (0x70, "F1"), (0x71, "F2"), (0x72, "F3"), (0x73, "F4"),
        (0x74, "F5"), (0x75, "F6"), (0x76, "F7"), (0x77, "F8"),
        (0x78, "F9"), (0x79, "F10"), (0x7A, "F11"), (0x7B, "F12"),
        (0x24, "Home"), (0x23, "End"), (0x2D, "Insert"),
    ];

    public bool IsValid
    {
        get
        {
            var key = this.Key;
            return Choices.Any(c => c.Key == key) && !(this.Alt && key is 0x09 or 0x73);
        }
    }

    public bool MatchesModifiers(bool control, bool alt, bool shift, bool windows) =>
        !windows && this.Control == control && this.Alt == alt && this.Shift == shift;

    public override string ToString()
    {
        var key = this.Key;
        return (this.Control ? "Ctrl+" : "") + (this.Alt ? "Alt+" : "") + (this.Shift ? "Shift+" : "")
            + Choices.FirstOrDefault(c => c.Key == key).Name;
    }
}
