// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one branch of a session's append-only message history. A
/// session normally has one active branch, but branching lets a durable
/// history fork — for example, to explore an alternate continuation from an
/// earlier point — without mutating or losing the original sequence of
/// messages.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// Every <see cref="AgentMessage"/> records the <see cref="BranchId"/> it
/// belongs to, and a history cursor pairs a <see cref="BranchId"/> with a
/// <see cref="SessionVersion"/> and <see cref="SessionSequence"/> to
/// describe a specific, reproducible point in that branch's history.
/// Switching a session's active branch changes
/// which messages new context assembly and new messages attach to, but it
/// never deletes or renumbers messages recorded on another branch.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="BranchId"/> always addresses a real branch.
/// </para>
/// </remarks>
public readonly record struct BranchId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BranchId"/> struct,
    /// validating that it addresses a real branch rather than an empty
    /// placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real branch.
    /// </exception>
    public BranchId(Guid value)
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
