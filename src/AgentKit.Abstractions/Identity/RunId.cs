// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one isolated run of an agent definition against a session.
/// Every piece of mutable execution state — turn number, budget
/// reservations, cancellation, pending tool calls — is scoped to exactly one
/// <see cref="RunId"/> and never leaks into another run, even a concurrent
/// run of the same <see cref="AgentId"/> and <see cref="SessionId"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself, so it is safe to
/// pass between threads, log, and use as a correlation key concurrently.
/// </para>
/// <para>
/// A run's identity is created once, at the moment a run starts, by the
/// engine's run-scope factory, and it is threaded through every subsequent
/// event, message, tool call, and durable checkpoint produced by that run so
/// they can all be traced back to the run that caused them. It is never
/// reused after the run settles, even if the run is later "continued" — a
/// continuation reuses the same durable session but always starts a new
/// <see cref="RunId"/> unless a durable executor is resuming the exact
/// interrupted run.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="RunId"/> always addresses a real run.
/// </para>
/// </remarks>
public readonly record struct RunId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunId"/> struct,
    /// validating that it addresses a real run rather than an empty
    /// placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real run.
    /// </exception>
    public RunId(Guid value)
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
