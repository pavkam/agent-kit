// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// Bounds text to a character ceiling by keeping a leading and trailing
/// portion around a marker, shared by every first-party strategy so an
/// extractive checkpoint, a model transcript, and a model summary are all
/// truncated with identical, surrogate-safe mechanics.
/// </summary>
/// <remarks>
/// The head and tail are kept on the theory that the beginning (initial
/// context) and end (most recent developments) of a covered range are
/// typically more load-bearing than its middle for future turns. Truncation
/// never splits a UTF-16 surrogate pair: a cut that would leave a lone high
/// surrogate at the end of the head or a lone low surrogate at the start of
/// the tail backs off by one code unit.
/// </remarks>
internal static class CompactionTextTruncation
{
    /// <summary>
    /// Returns <paramref name="text"/> unchanged when it fits within <paramref name="maximumCharacters"/>, and
    /// otherwise the head and tail of <paramref name="text"/> joined by <paramref name="marker"/> so the result is at
    /// most <paramref name="maximumCharacters"/> UTF-16 code units long.
    /// </summary>
    /// <param name="text">The text to bound.</param>
    /// <param name="maximumCharacters">The inclusive ceiling on the result length in UTF-16 code units.</param>
    /// <param name="marker">The marker inserted between the retained head and tail.</param>
    /// <param name="truncated">Set to <see langword="true"/> when text was removed; otherwise <see langword="false"/>.</param>
    /// <returns>
    /// The bounded text. When <paramref name="maximumCharacters"/> does not exceed the marker's length no room remains
    /// for any retained text, so the input is returned unchanged and <paramref name="truncated"/> is
    /// <see langword="false"/>; callers validate their ceilings against their markers at composition.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="marker"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumCharacters"/> is negative.</exception>
    internal static string KeepHeadAndTail(string text, int maximumCharacters, string marker, out bool truncated)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(marker);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCharacters);

        truncated = false;
        if (text.Length <= maximumCharacters || maximumCharacters <= marker.Length)
        {
            return text;
        }

        var remaining = maximumCharacters - marker.Length;
        var headLength = remaining / 2;
        var tailLength = remaining - headLength;

        // A head ending on a high surrogate or a tail starting on a low surrogate would emit a lone surrogate.
        if (headLength > 0 && char.IsHighSurrogate(text[headLength - 1]))
        {
            headLength--;
        }

        if (tailLength > 0 && char.IsLowSurrogate(text[^tailLength]))
        {
            tailLength--;
        }

        truncated = true;
        return string.Concat(text.AsSpan(0, headLength), marker, text.AsSpan(text.Length - tailLength));
    }
}
