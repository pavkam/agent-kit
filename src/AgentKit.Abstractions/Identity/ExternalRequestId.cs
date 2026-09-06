// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A caller- or transport-supplied request identifier, such as an inbound
/// HTTP correlation ID or a message-queue envelope ID, preserved as
/// external correlation rather than AgentKit identity.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// A channel adapter (HTTP, console, message queue) commonly receives a
/// caller-supplied or transport-generated correlation identifier before
/// AgentKit ever assigns its own identities. Capturing that value as an
/// <see cref="ExternalRequestId"/> lets logs and traces connect "the
/// request that arrived on the wire" with "the run it eventually caused"
/// without conflating the two identity spaces or requiring the transport to
/// understand AgentKit's own <see cref="RunId"/>/<see cref="TurnId"/>
/// scheme.
/// </para>
/// </remarks>
public readonly record struct ExternalRequestId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalRequestId"/>
    /// struct, validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty external request identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ExternalRequestId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the external request identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the external identifier text.</summary>
    public override string ToString() => Value;
}
