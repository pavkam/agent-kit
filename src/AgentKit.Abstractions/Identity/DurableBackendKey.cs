// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Selects one registered durable execution backend, such as a workflow
/// engine or durable task system, from the engine-wide backend catalog.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// Backend selection never silently falls back. A record that names a backend
/// key which is absent from the current composition fails validation rather
/// than resuming against a different engine, because the two engines do not
/// share handoff state, idempotency semantics, or reconciliation behavior.
/// </para>
/// </remarks>
public readonly record struct DurableBackendKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableBackendKey"/>
    /// struct, validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty canonical backend key.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurableBackendKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical backend key text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical backend key text, suitable for logging and
    /// composition-validation messages.
    /// </summary>
    public override string ToString() => Value;
}
