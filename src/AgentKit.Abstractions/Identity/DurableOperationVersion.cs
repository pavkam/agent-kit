// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The version of one <see cref="DurableOperationName"/>'s serialized input,
/// state, and result contract, used to decide whether a recovering worker can
/// safely interpret a previously recorded payload.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// Versions are compared for exact equality rather than ordered, because a
/// codec either understands a recorded payload or it does not; there is no
/// general rule that a higher version can read a lower one. When the running
/// code cannot interpret a recorded version, recovery must migrate the
/// payload explicitly, pin the old operation version, or stop with a typed
/// incompatibility result. Silently reinterpreting recorded input under new
/// code is forbidden.
/// </para>
/// </remarks>
public readonly record struct DurableOperationVersion
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableOperationVersion"/> struct, validating that it
    /// carries usable version text.
    /// </summary>
    /// <param name="value">The non-empty canonical operation version.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurableOperationVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical operation version text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical operation version text, suitable for logging and
    /// recovery-incompatibility messages.
    /// </summary>
    public override string ToString() => Value;
}
