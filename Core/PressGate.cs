namespace KeyboundTab.Core;

/// <summary>A held key cannot retrigger, even after focus or modifier changes.</summary>
public sealed class PressGate
{
    private bool wasDown = true;

    public bool Update(bool down, bool eligible)
    {
        var pressed = down && !this.wasDown;
        this.wasDown = down;
        return eligible && pressed;
    }

    public void Reset() => this.wasDown = true;
}
