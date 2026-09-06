// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A stable, human-readable selection key for one keyed dependency-injection
/// registration of <typeparamref name="TContract"/> — for example, "which
/// registered <c>IAgentLoop</c> should this agent definition use?"
/// </summary>
/// <typeparam name="TContract">
/// The service contract this key selects among several possible
/// registrations.
/// </typeparam>
/// <remarks>
/// <para>
/// An agent definition selects behavior that is already registered in
/// dependency injection through typed keys like this one; it never embeds a
/// service instance directly. This indirection is what lets one engine host
/// several agent definitions that each choose a different keyed loop,
/// context assembler, model selector, or store from the same shared
/// container, without the definition itself depending on Microsoft DI types.
/// </para>
/// <para>
/// <see cref="ComponentKey{TContract}"/> is generic over the contract type
/// so that a key string is only ever comparable to another key for the same
/// contract: a key of <c>ComponentKey&lt;IAgentLoop&gt;</c> and a key of
/// <c>ComponentKey&lt;IContextAssembler&gt;</c> with identical text are
/// distinct values and cannot be confused with each other at compile time,
/// even though both ultimately wrap a plain string.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/> (and the closed <typeparamref name="TContract"/>). It
/// carries no mutable state and is safe to share and compare across threads
/// without synchronization.
/// </para>
/// </remarks>
public readonly record struct ComponentKey<TContract>
    where TContract : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentKey{TContract}"/>
    /// struct, validating that it carries a usable selection key rather
    /// than an empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty canonical key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ComponentKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>
    /// Gets the canonical key text used to look up the selected
    /// registration in the component catalog for
    /// <typeparamref name="TContract"/>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical key text, suitable for logging and diagnostic
    /// messages that report which keyed selection was used or missing.
    /// </summary>
    public override string ToString() => Value;
}
