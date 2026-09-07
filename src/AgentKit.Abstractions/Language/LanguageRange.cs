// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a half-open portable source range.</summary>
public readonly record struct LanguageRange
{
    /// <summary>Initializes an ordered half-open range.</summary>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position, not before <paramref name="start"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="end"/> precedes <paramref name="start"/>.</exception>
    public LanguageRange(LanguagePosition start, LanguagePosition end)
    {
        if (end.Line < start.Line || (end.Line == start.Line && end.Character < start.Character))
        {
            throw new ArgumentException("The end position must not precede the start position.", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>Gets the inclusive start position.</summary>
    public LanguagePosition Start { get; }
    /// <summary>Gets the exclusive end position.</summary>
    public LanguagePosition End { get; }
}
