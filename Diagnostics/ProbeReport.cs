namespace KeyboundTab.Diagnostics;

public sealed record NodeReport(uint Id, string Type, bool Visible, string? Text, NodeReport[]? Children, float X = 0, float Y = 0);
public sealed record AddonReport(string Name, ushort Id, bool Visible, bool Ready, NodeReport[] Nodes, long VirtualTableRva = 0);
public sealed record InputReport(bool Found, bool Enabled, bool Active, bool Focused, string Raw, string Evaluated, bool Blocked = false, uint ComponentFlags = 0);
public sealed record ActivityReport(float InputSeconds, float ContentSeconds, float AfkSeconds, float ContentLimit, int Status);
public sealed record EnemyReport(uint Id, string Word, string Reading, string Skill, int[] Data)
{
    public bool IsCasting => this.Id is not (0 or 0xE0000000) && this.Skill.Length > 0 && this.Data.Length == 13 && this.Data[7] > 0;
}
public sealed record ProbeReport(string Note, InputReport Input, AddonReport[] Addons, ActivityReport? Activity = null, EnemyReport[]? Enemies = null);
