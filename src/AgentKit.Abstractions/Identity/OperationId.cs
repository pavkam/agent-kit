// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one causal operation for correlation across before-run,
/// in-run, and after-run boundaries. Every <see cref="OperationCorrelation"/>
/// value — <see cref="BeforeRunOperationCorrelation"/>,
/// <see cref="InRunOperationCorrelation"/>, or
/// <see cref="AfterRunOperationCorrelation"/> — carries exactly one
/// <see cref="OperationId"/> that stays stable across that operation's
/// lifetime, regardless of which run boundary it started or ended in.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// Operations frequently outlive a single run: a deferred approval may be
/// requested during one run and resolved after that run has already
/// settled, or a durable follow-up may be scheduled by one run and executed
/// by a later one. <see cref="OperationId"/> is what lets audit records,
/// hooks, and observability correlate "this decision" or "this durable
/// effect" back to the operation that caused it, independent of which
/// <see cref="RunId"/> happened to be active at each boundary.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="OperationId"/> always addresses a real operation.
/// </para>
/// </remarks>
public readonly record struct OperationId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationId"/> struct,
    /// validating that it addresses a real operation rather than an empty
    /// placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real operation.
    /// </exception>
    public OperationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique identifier. This is the only
    /// state the type carries, and it never changes after construction.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Returns the canonical, lowercase, hyphenated text form of this
    /// identity (GUID "D" format), suitable for logging, storage keys, and
    /// round-tripping through configuration or external protocols.
    /// </summary>
    public override string ToString() => Value.ToString("D");
}
