// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The deterministic name of a kind of recoverable operation, such as a model
/// request or a tool call, used to dispatch recovery to the correct handler
/// and codec.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// The name is part of the durable record and must remain stable across
/// deployments. Renaming an operation is a breaking change to recovery: a
/// worker loading an older journal entry looks the handler up by this name,
/// so a rename orphans in-flight work rather than silently migrating it.
/// Behavioral changes are expressed through
/// <see cref="DurableOperationVersion"/> instead.
/// </para>
/// </remarks>
public readonly record struct DurableOperationName
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableOperationName"/> struct, validating that it carries
    /// usable name text.
    /// </summary>
    /// <param name="value">The non-empty canonical operation name.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurableOperationName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical operation name.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical operation name, suitable for logging, tracing,
    /// and recovery diagnostics.
    /// </summary>
    public override string ToString() => Value;
}
