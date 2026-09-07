// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Describes safe diagnostics for a non-retriable schema configuration failure.</summary>
public sealed record OutputSchemaConfigurationFailure
{
    /// <summary>Initializes configuration failure diagnostics.</summary>
    /// <param name="kind">The defined failure category.</param>
    /// <param name="safeMessage">The nonblank safe explanation.</param>
    /// <param name="issues">The initialized issue list bounded by the runtime profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank or <paramref name="issues"/> is default or contains <see langword="null"/>.</exception>
    public OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind kind, string safeMessage, ImmutableArray<OutputValidationIssue> issues)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentException.ThrowIfDefault(issues);
        ArgumentException.ThrowIfContainsNull(issues);

        Kind = kind;
        SafeMessage = safeMessage;
        Issues = issues;
    }
    /// <summary>Gets category.</summary><value>A defined failure category.</value>
    public OutputSchemaConfigurationFailureKind Kind { get; }
    /// <summary>Gets safe explanation.</summary><value>Non-sensitive text.</value>
    public string SafeMessage { get; }
    /// <summary>Gets bounded diagnostics.</summary><value>An initialized immutable array.</value>
    public ImmutableArray<OutputValidationIssue> Issues { get; }

    /// <inheritdoc/>
    public bool Equals(OutputSchemaConfigurationFailure? other) =>
        other is not null
        && Kind == other.Kind
        && StringComparer.Ordinal.Equals(SafeMessage, other.SafeMessage)
        && Issues.SequenceEqual(other.Issues);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(SafeMessage, StringComparer.Ordinal);
        foreach (var issue in Issues)
        {
            hash.Add(issue);
        }

        return hash.ToHashCode();
    }
}
