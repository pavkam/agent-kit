// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Why one output candidate did not validate.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record OutputValidationFailure
{
    /// <summary>Initializes a new instance of the <see cref="OutputValidationFailure"/> record.</summary>
    /// <param name="kind">The category of this failure.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="issues">The individual diagnostics that contributed to this failure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace, or
    /// <paramref name="issues"/> is a default, uninitialized array.
    /// </exception>
    public OutputValidationFailure(
        OutputValidationFailureKind kind, string safeMessage, ImmutableArray<OutputValidationIssue> issues)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentException.ThrowIfDefault(issues);

        Kind = kind;
        SafeMessage = safeMessage;
        Issues = issues;
    }

    /// <summary>Gets the category of this failure.</summary>
    public OutputValidationFailureKind Kind { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }

    /// <summary>Gets the individual diagnostics that contributed to this failure.</summary>
    public ImmutableArray<OutputValidationIssue> Issues { get; init; }

    /// <inheritdoc/>
    public bool Equals(OutputValidationFailure? other) =>
        other is not null
        && Kind == other.Kind
        && SafeMessage == other.SafeMessage
        && Issues.SequenceEqual(other.Issues);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(SafeMessage);
        foreach (var issue in Issues)
        {
            hash.Add(issue);
        }

        return hash.ToHashCode();
    }
}
