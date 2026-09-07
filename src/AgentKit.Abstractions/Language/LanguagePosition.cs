// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a zero-based UTF-16 line and character position used by portable language services.</summary>
public readonly record struct LanguagePosition
{
    /// <summary>Initializes a non-negative zero-based position.</summary>
    /// <param name="line">The zero-based line index.</param>
    /// <param name="character">The zero-based UTF-16 character index.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either coordinate is negative.</exception>
    public LanguagePosition(int line, int character)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfNegative(character);
        Line = line;
        Character = character;
    }

    /// <summary>Gets the zero-based line index.</summary>
    public int Line { get; }
    /// <summary>Gets the zero-based UTF-16 character index.</summary>
    public int Character { get; }
}
