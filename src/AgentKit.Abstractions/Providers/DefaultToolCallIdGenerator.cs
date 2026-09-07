// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A general-purpose default <see cref="IIdentifierGenerator{ToolCallId}"/>
/// that any provider integration package may register, minting a new
/// random <see cref="ToolCallId"/> for every provider-requested tool call
/// its response parser observes.
/// </summary>
/// <remarks>
/// This is a replaceable default, not an engine-wide fixture: an
/// application that registers its own
/// <see cref="IIdentifierGenerator{ToolCallId}"/> (for example, one backed
/// by a deterministic or cryptographically hardened source) replaces this
/// registration through the ordinary <c>TryAdd</c> first-registration-wins
/// rule simply by registering its own implementation before the consuming
/// provider package's own registration entry point runs.
/// </remarks>
public sealed class DefaultToolCallIdGenerator: IIdentifierGenerator<ToolCallId>
{
    /// <inheritdoc/>
    public ToolCallId Create() => new(Guid.NewGuid());
}
