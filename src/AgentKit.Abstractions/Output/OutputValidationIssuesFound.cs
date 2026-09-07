// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The validator raised one or more issues against the candidate.</summary>
public sealed record OutputValidationIssuesFound: OutputValidationResult
{
    /// <summary>Initializes a new instance of the <see cref="OutputValidationIssuesFound"/> record.</summary>
    /// <param name="issues">The non-empty set of issues raised.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="issues"/> is a default, uninitialized array, or is empty.
    /// </exception>
    public OutputValidationIssuesFound(ImmutableArray<OutputValidationIssue> issues)
    {
        ArgumentException.ThrowIfDefault(issues);
        if (issues.IsEmpty)
        {
            throw new ArgumentException("At least one issue is required.", nameof(issues));
        }

        Issues = issues;
    }

    /// <summary>Gets the issues raised.</summary>
    public ImmutableArray<OutputValidationIssue> Issues { get; init; }

    /// <inheritdoc/>
    public bool Equals(OutputValidationIssuesFound? other) =>
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
