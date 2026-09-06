// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A candidate-validation attempt that found one or more disqualifying issues.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionValidationRejected: CompactionValidationResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionValidationRejected"/> record.</summary>
    /// <param name="issues">The disqualifying issues found, in evaluation order.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="issues"/> is a default, uninitialized array, or is
    /// empty.
    /// </exception>
    public CompactionValidationRejected(ImmutableArray<CompactionValidationIssue> issues)
    {
        ArgumentException.ThrowIfDefault(issues);
        if (issues.IsEmpty)
        {
            throw new ArgumentException("Issues must contain at least one entry.", nameof(issues));
        }

        Issues = issues;
    }

    /// <summary>Gets the disqualifying issues found, in evaluation order.</summary>
    public ImmutableArray<CompactionValidationIssue> Issues { get; init; }

    /// <inheritdoc/>
    public bool Equals(CompactionValidationRejected? other) =>
        other is not null && Issues.SequenceEqual(other.Issues);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var issue in Issues)
        {
            hash.Add(issue);
        }

        return hash.ToHashCode();
    }
}
