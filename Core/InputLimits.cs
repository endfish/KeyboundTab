namespace KeyboundTab.Core;

public static class InputLimits
{
    // KTGInput has a 96-byte limit and MaxChar=0 (no separate character limit).
    public static bool Fits(int byteCount, int runeCount, uint maxBytes, uint maxChars) =>
        byteCount > 0 && byteCount <= 4096 && maxBytes > 0 && byteCount <= maxBytes
        && (maxChars == 0 || runeCount <= maxChars);
}
