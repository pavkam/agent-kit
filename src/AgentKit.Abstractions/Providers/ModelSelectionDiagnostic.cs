// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An explanation of why one candidate alias was skipped or accepted during
/// selection.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Diagnostics exist so that "no compatible model" is actionable. Without
/// per-candidate reasons the only available answer to a failed selection is
/// that nothing matched, which does not tell an operator whether an alias was
/// misspelled, absent from the catalog, or merely lacking one capability.
/// </para>
/// </remarks>
public sealed record ModelSelectionDiagnostic
{
    private readonly string _reason;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ModelSelectionDiagnostic"/> record.
    /// </summary>
    /// <param name="alias">The candidate this diagnostic describes.</param>
    /// <param name="outcome">What happened to the candidate.</param>
    /// <param name="reason">
    /// A redacted, human-readable explanation. It must not contain prompts,
    /// model output, or credentials.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a defined enumeration value.
    /// </exception>
    public ModelSelectionDiagnostic(
        ModelAlias alias,
        ModelCandidateOutcome outcome,
        string reason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Alias = alias;
        Outcome = outcome;
        _reason = reason;
    }

    /// <summary>Gets the candidate this diagnostic describes.</summary>
    public ModelAlias Alias { get; init; }

    /// <summary>Gets what happened to the candidate.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public ModelCandidateOutcome Outcome
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Outcome));
            field = value;
        }
    }

    /// <summary>Gets the redacted explanation.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string Reason
    {
        get => _reason;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Reason));
            _reason = value;
        }
    }
}
