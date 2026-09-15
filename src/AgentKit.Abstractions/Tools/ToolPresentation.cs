// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contains bounded immutable application-facing presentation parts for one tool call stage.</summary>
public sealed record ToolPresentation
{
    /// <summary>Initializes a presentation.</summary>
    /// <param name="parts">The initialized ordered parts.</param>
    /// <param name="disposition">The defined production disposition.</param>
    /// <param name="omittedCharacters">A nonnegative lower bound on literal characters omitted by bounding.</param>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is default or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="disposition"/> is undefined or <paramref name="omittedCharacters"/> is negative.</exception>
    public ToolPresentation(ImmutableArray<ToolPresentationPart> parts, ToolPresentationDisposition disposition, long omittedCharacters = 0)
    {
        ArgumentException.ThrowIfDefault(parts);
        ArgumentException.ThrowIfContainsNull(parts, nameof(parts));
        ArgumentOutOfRangeException.ThrowIfUndefined(disposition);
        ArgumentOutOfRangeException.ThrowIfNegative(omittedCharacters);
        Parts = parts; Disposition = disposition; OmittedCharacters = omittedCharacters;
    }

    /// <summary>Gets the ordered literal parts.</summary><value>An initialized immutable array.</value>
    public ImmutableArray<ToolPresentationPart> Parts { get; }
    /// <summary>Gets how the output was produced.</summary><value>A defined disposition.</value>
    public ToolPresentationDisposition Disposition { get; }
    /// <summary>Gets the known lower bound on omitted characters.</summary><value>Zero for complete output; otherwise a nonnegative lower bound that does not require scanning discarded input.</value>
    public long OmittedCharacters { get; }
}
