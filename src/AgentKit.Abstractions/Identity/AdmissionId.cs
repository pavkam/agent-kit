// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one admission receipt produced when input is accepted for
/// later promotion into a turn. The receipt is the caller-visible proof that
/// admission happened, distinct from the <see cref="InputId"/> of the
/// content that was admitted and distinct from whichever turn eventually
/// promotes it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// Separating admission from promotion is what makes safe steering and
/// idempotent resubmission possible: a caller can retry a submission with
/// the same idempotency key and observe the same admission outcome without
/// duplicating the underlying input, and later components can reference
/// "the admission that caused this" (see
/// <see cref="BeforeRunOperationCorrelation"/>) without conflating it with
/// the run that eventually consumes the admitted input.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="AdmissionId"/> always addresses a real receipt.
/// </para>
/// </remarks>
public readonly record struct AdmissionId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdmissionId"/> struct,
    /// validating that it addresses a real admission receipt rather than an
    /// empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real receipt.
    /// </exception>
    public AdmissionId(Guid value)
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
