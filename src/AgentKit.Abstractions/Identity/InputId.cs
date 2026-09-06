// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one unit of input submitted for admission into a session,
/// before it is known whether that input will be accepted, queued, rejected,
/// or merged with other pending input.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// Accepting input and promoting it into a turn are deliberately separate
/// operations: an <see cref="InputId"/> identifies the submitted unit of
/// input itself, independent of the <see cref="AdmissionId"/> issued as a
/// receipt for that submission and independent of whichever
/// <see cref="RunId"/>/<see cref="TurnId"/> eventually consumes it (if any).
/// Keeping these identities distinct is what lets the I/O component make
/// idempotent acceptance, steering, and queued-follow-up decisions without
/// losing track of exactly which input a later decision refers to.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="InputId"/> always addresses a real submission.
/// </para>
/// </remarks>
public readonly record struct InputId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InputId"/> struct,
    /// validating that it addresses a real input submission rather than an
    /// empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real submission.
    /// </exception>
    public InputId(Guid value)
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
