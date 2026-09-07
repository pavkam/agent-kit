// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that a candidate violated a valid, preflighted schema.</summary>
public sealed record OutputSchemaCandidateInvalid: OutputSchemaEvaluationResult
{
    /// <summary>Initializes a candidate-validation failure.</summary>
    /// <param name="issues">The initialized, nonempty validation issue collection.</param>
    /// <exception cref="ArgumentException"><paramref name="issues"/> is default, empty, or contains <see langword="null"/>.</exception>
    public OutputSchemaCandidateInvalid(ImmutableArray<OutputValidationIssue> issues)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(issues);
        ArgumentException.ThrowIfContainsNull(issues);
        Issues = issues;
    }

    /// <summary>Gets the bounded validation issues reported for the candidate.</summary>
    /// <value>An initialized, nonempty immutable collection.</value>
    public ImmutableArray<OutputValidationIssue> Issues { get; }

    /// <inheritdoc/>
    public bool Equals(OutputSchemaCandidateInvalid? other) =>
        other is not null && Issues.SequenceEqual(other.Issues);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var issue in Issues)
        {
            hash.Add(issue);
        }

        return hash.ToHashCode();
    }
}
