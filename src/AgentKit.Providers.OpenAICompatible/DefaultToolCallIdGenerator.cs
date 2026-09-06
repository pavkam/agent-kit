// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The default <see cref="IIdentifierGenerator{ToolCallId}"/> registered by
/// <see cref="ServiceExtensions.AddOpenAICompatibleProvider"/>, minting a
/// new random <see cref="ToolCallId"/> for every provider-requested tool
/// call this package's response parser observes.
/// </summary>
/// <remarks>
/// This is a package-local, replaceable default: an application that
/// registers its own engine-wide
/// <see cref="IIdentifierGenerator{ToolCallId}"/> (for example, one backed
/// by a deterministic or cryptographically hardened source) replaces this
/// registration through the ordinary <c>TryAdd</c> first-registration-wins
/// rule simply by registering its own implementation before calling
/// <see cref="ServiceExtensions.AddOpenAICompatibleProvider"/>.
/// </remarks>
public sealed class DefaultToolCallIdGenerator: IIdentifierGenerator<ToolCallId>
{
    /// <inheritdoc/>
    public ToolCallId Create() => new(Guid.NewGuid());
}
