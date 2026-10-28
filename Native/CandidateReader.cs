using KeyboundTab.Core;
using KeyboundTab.Diagnostics;

namespace KeyboundTab.Native;

internal static class CandidateReader
{
    public static WordCandidate[] Read(ProbeReport report, WordCatalog catalog, bool prioritizeCasting = true)
    {
        var result = new List<WordCandidate>();
        var casting = prioritizeCasting ? (report.Enemies ?? []).Where(e => e.IsCasting).Select(e => e.Word).ToHashSet(StringComparer.Ordinal) : [];
        foreach (var addon in report.Addons.Where(a => a.Name == "KTGEWord" && a.Visible))
        {
            // One addon holds the template plus duplicated components (20001, ...).
            // Every visible component can contribute a word, including boss/add waves.
            foreach (var component in addon.Nodes.Where(n => n.Visible && n.Children != null))
            {
                var display = component.Children!.FirstOrDefault(n => n.Id == 20 && n.Visible)?.Text;
                if (display != null && catalog.MatchChinese(display) is { } word)
                    result.Add(word with { Priority = casting.Contains(display) ? 1 : 0 });
            }
        }
        return result.DistinctBy(w => w.Input, StringComparer.Ordinal).ToArray();
    }
}
