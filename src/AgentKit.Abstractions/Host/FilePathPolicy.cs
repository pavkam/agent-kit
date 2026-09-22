// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Lexical path comparison and normalization policy for one file-system profile.</summary>
public sealed record FilePathPolicy
{
    /// <summary>Initializes a new instance of the <see cref="FilePathPolicy"/> record.</summary>
    /// <param name="comparisonKind">The comparison semantics for path segments.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="comparisonKind"/> is undefined.</exception>
    public FilePathPolicy(FilePathComparisonKind comparisonKind)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(comparisonKind);
        ComparisonKind = comparisonKind;
    }

    /// <summary>Gets the comparison semantics for path segments.</summary>
    public FilePathComparisonKind ComparisonKind { get; init; }
}
