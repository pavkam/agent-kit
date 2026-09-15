// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds input inspection and application-facing output for one presentation operation.</summary>
public sealed record ToolPresentationBounds
{
    /// <summary>Initializes positive presentation limits.</summary>
    /// <param name="maximumInputBytes">The maximum strict UTF-8 source bytes inspected before fallback.</param>
    /// <param name="maximumOutputCharacters">The maximum UTF-16 characters returned across all parts.</param>
    /// <param name="maximumParts">The maximum returned part count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any limit is not positive.</exception>
    public ToolPresentationBounds(int maximumInputBytes = 262_144, int maximumOutputCharacters = 32_768, int maximumParts = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInputBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumParts);
        MaximumInputBytes = maximumInputBytes; MaximumOutputCharacters = maximumOutputCharacters; MaximumParts = maximumParts;
    }

    /// <summary>Gets the source-byte inspection limit.</summary><value>A positive byte count.</value>
    public int MaximumInputBytes { get; }
    /// <summary>Gets the aggregate returned-character limit.</summary><value>A positive character count.</value>
    public int MaximumOutputCharacters { get; }
    /// <summary>Gets the returned-part limit.</summary><value>A positive part count.</value>
    public int MaximumParts { get; }
}
