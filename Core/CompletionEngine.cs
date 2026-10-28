namespace KeyboundTab.Core;

public sealed record WordCandidate(string Display, string Input, int Priority = 0);

/// <summary>Only selects from a fresh snapshot of currently available words.</summary>
public sealed class CompletionEngine
{
    private string? lastFilled;
    private string? lastContext;
    private readonly HashSet<string> previewed = new(StringComparer.Ordinal);

    public WordCandidate? Select(string input, IReadOnlyList<WordCandidate> candidates, string context, Random random)
    {
        var available = candidates.Where(c => !string.IsNullOrWhiteSpace(c.Input))
            .GroupBy(c => c.Input, StringComparer.Ordinal).Select(g => g.MaxBy(c => c.Priority)!).ToArray();
        if (available.Length == 0)
            return null;

        var reroll = this.lastContext == context && this.lastFilled == input;
        if (reroll)
        {
            // A sustained boss cast must never lock the player out of the other
            // words. Repeated gestures preview every current word before repeating.
            var alternatives = available.Length > 1 ? available.Where(c => c.Input != input).ToArray() : available;
            var unseen = alternatives.Where(c => !this.previewed.Contains(c.Input)).ToArray();
            if (unseen.Length == 0)
            {
                this.previewed.Clear();
                unseen = alternatives;
            }
            return Choose(unseen, random);
        }
        if (input.Length > 0 && !reroll)
        {
            var matches = available.Where(c => c.Input.StartsWith(input, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 0)
                return Choose(matches, random);
        }

        return Choose(available, random);
    }

    private static WordCandidate Choose(WordCandidate[] candidates, Random random)
    {
        if (candidates.All(c => c.Priority == candidates[0].Priority))
            return candidates[random.Next(candidates.Length)];

        // Priority is a preference, not an exclusion. Every word retains a ticket;
        // a casting target receives one extra ticket, regardless of its rank.
        var ticket = random.Next(candidates.Sum(c => c.Priority > 0 ? 2 : 1));
        foreach (var candidate in candidates)
        {
            ticket -= candidate.Priority > 0 ? 2 : 1;
            if (ticket < 0)
                return candidate;
        }
        throw new InvalidOperationException("Invalid completion lottery result.");
    }

    public void ConfirmWrite(string text, string context)
    {
        if (context != this.lastContext)
            this.previewed.Clear();
        this.lastFilled = text;
        this.lastContext = context;
        this.previewed.Add(text);
    }

    // Called for observed manual changes, focus loss, addon closure, and binding changes.
    public void Observe(string input, string context)
    {
        if (context != this.lastContext || input != this.lastFilled)
            this.Reset();
    }

    public void Reset()
    {
        this.lastFilled = null;
        this.lastContext = null;
        this.previewed.Clear();
    }
}
