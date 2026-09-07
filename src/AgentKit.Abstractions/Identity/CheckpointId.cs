// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The stable identity of one durably recorded checkpoint describing the
/// complete state of a recoverable operation at a semantic boundary.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// Framework-created checkpoint identities come from the registered
/// <see cref="IIdentifierGenerator{TIdentifier}"/> for this type so that
/// recovery tests can supply a deterministic sequence. Deserializing an
/// existing checkpoint preserves its recorded identity and never generates a
/// replacement as repair, because the identity is part of the durable
/// evidence a recovering worker matches against.
/// </para>
/// </remarks>
public readonly record struct CheckpointId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointId"/> struct,
    /// validating that it carries a usable, non-empty identity.
    /// </summary>
    /// <param name="value">The non-empty checkpoint identity.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which is the
    /// default value and never identifies a recorded checkpoint.
    /// </exception>
    public CheckpointId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying checkpoint identity.</summary>
    public Guid Value { get; }

    /// <summary>
    /// Returns the checkpoint identity in its canonical form, suitable for
    /// logging, tracing, and recovery diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString();
}
