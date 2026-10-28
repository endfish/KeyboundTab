using Dalamud.Configuration;

namespace KeyboundTab;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public bool Enabled { get; set; } = true;
    public bool PrioritizeCasting { get; set; } = true;
    public int Key { get; set; } = 0x09;
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
}
