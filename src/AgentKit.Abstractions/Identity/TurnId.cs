// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one turn within a <see cref="RunId"/>. A turn is one
/// iteration of the agent loop's state machine: it typically spans a single
/// model request plus the tool calls that request produces, and a run
/// normally advances through several turns before it settles.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// A new <see cref="TurnId"/> is minted each time the loop begins another
/// iteration, and it correlates the messages, tool calls, and events
/// produced during that iteration so replay and observability can
/// reconstruct exactly which turn caused which effect, independent of the
/// monotonically increasing turn/step number also tracked by the run's
/// context.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="TurnId"/> always addresses a real turn.
/// </para>
/// </remarks>
public readonly record struct TurnId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TurnId"/> struct,
    /// validating that it addresses a real turn rather than an empty
    /// placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real turn.
    /// </exception>
    public TurnId(Guid value)
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
