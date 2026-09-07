// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A record of how one accepted output candidate was processed.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record OutputValidationManifest
{
    /// <summary>Initializes a new instance of the <see cref="OutputValidationManifest"/> record.</summary>
    /// <param name="definitionId">The definition this candidate was validated against.</param>
    /// <param name="mode">The output mode this candidate was processed under.</param>
    /// <param name="validationAttempt">The one-based attempt number this manifest describes.</param>
    /// <param name="issues">Non-blocking diagnostics observed while accepting this candidate, if any.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mode"/> is undefined, or <paramref name="validationAttempt"/> is less than one.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="issues"/> is a default, uninitialized array.</exception>
    public OutputValidationManifest(
        OutputDefinitionId definitionId,
        OutputMode mode,
        int validationAttempt,
        ImmutableArray<OutputValidationIssue> issues)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentOutOfRangeException.ThrowIfLessThan(validationAttempt, 1);
        ArgumentException.ThrowIfDefault(issues);

        DefinitionId = definitionId;
        Mode = mode;
        ValidationAttempt = validationAttempt;
        Issues = issues;
    }

    /// <summary>Gets the definition this candidate was validated against.</summary>
    public OutputDefinitionId DefinitionId { get; init; }

    /// <summary>Gets the output mode this candidate was processed under.</summary>
    public OutputMode Mode { get; init; }

    /// <summary>Gets the one-based attempt number this manifest describes.</summary>
    public int ValidationAttempt { get; init; }

    /// <summary>Gets non-blocking diagnostics observed while accepting this candidate, if any.</summary>
    public ImmutableArray<OutputValidationIssue> Issues { get; init; }

    /// <inheritdoc/>
    public bool Equals(OutputValidationManifest? other) =>
        other is not null
        && DefinitionId.Equals(other.DefinitionId)
        && Mode == other.Mode
        && ValidationAttempt == other.ValidationAttempt
        && Issues.SequenceEqual(other.Issues);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(DefinitionId);
        hash.Add(Mode);
        hash.Add(ValidationAttempt);
        foreach (var issue in Issues)
        {
            hash.Add(issue);
        }

        return hash.ToHashCode();
    }
}
