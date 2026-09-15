// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Text;

/// <summary>
/// Cuts recorded text response bodies at deterministic points so a test can serve a truthful prefix of a
/// streaming fixture, either as a cleanly truncated body or as the prefix of a
/// <see cref="FaultingReadStream"/>.
/// </summary>
public static class ResponseBodyFixture
{
    /// <summary>
    /// Returns the UTF-8 prefix of <paramref name="payload"/> that ends with the first line containing
    /// <paramref name="marker"/>, including that line's terminator and one blank separator line, so the last
    /// server-sent event in the prefix is complete and dispatchable.
    /// </summary>
    /// <param name="payload">The full recorded body.</param>
    /// <param name="marker">A substring that identifies the last line to keep; matched ordinally.</param>
    /// <returns>The prefix bytes ending after the marked line and a blank line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> or <paramref name="marker"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="marker"/> is empty or whitespace, or no line of <paramref name="payload"/> contains it.</exception>
    public static byte[] TruncateAfterLineContaining(byte[] payload, string marker)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        var text = Encoding.UTF8.GetString(payload);
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new ArgumentException($"No line of the payload contains '{marker}'.", nameof(marker));
        }

        var lineEnd = text.IndexOf('\n', markerIndex);
        var line = lineEnd < 0 ? text : text[..(lineEnd + 1)];
        return Encoding.UTF8.GetBytes(line.TrimEnd('\r', '\n') + "\n\n");
    }
}
