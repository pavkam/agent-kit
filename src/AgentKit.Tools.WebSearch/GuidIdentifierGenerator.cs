// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

/// <summary>Creates random GUID-backed identities for web-search network and security operations.</summary>
/// <typeparam name="TId">The dedicated identity value type to create.</typeparam>
/// <param name="factory">Wraps a fresh GUID in <typeparamref name="TId"/>.</param>
/// <remarks>This default is registered with <c>TryAdd</c> semantics so hosts and tests replace it through DI.</remarks>
internal sealed class GuidIdentifierGenerator<TId>(Func<Guid, TId> factory): IIdentifierGenerator<TId>
    where TId : struct
{
    /// <inheritdoc/>
    public TId Create() => factory(Guid.NewGuid());
}
