namespace KeyboundTab.Core;

public sealed class WordCatalog(IEnumerable<WordCandidate> entries)
{
    private readonly Dictionary<string, string[]> inputsByDisplay = entries
        .Where(w => !string.IsNullOrEmpty(w.Display) && !string.IsNullOrEmpty(w.Input))
        .GroupBy(w => w.Display, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.Select(w => w.Input).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.Ordinal);

    // Both on-screen fields must agree with one actual game-data row.
    // The catalog never supplies off-screen random candidates.
    public WordCandidate? Match(string display, string input) =>
        this.inputsByDisplay.TryGetValue(display, out var inputs)
        && inputs.Contains(input, StringComparer.OrdinalIgnoreCase)
            ? new WordCandidate(display, input)
            : null;

    // CN gameplay accepts committed Chinese text; the reading label is hidden.
    public WordCandidate? MatchChinese(string display) =>
        this.inputsByDisplay.ContainsKey(display) ? new WordCandidate(display, display) : null;
}
