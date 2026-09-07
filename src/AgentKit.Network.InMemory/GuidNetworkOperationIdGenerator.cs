// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>Creates collision-resistant network operation identities for deterministic adapter consumers.</summary>
internal sealed class GuidNetworkOperationIdGenerator: IIdentifierGenerator<NetworkOperationId>
{
    /// <inheritdoc/>
    public NetworkOperationId Create() => new(Guid.NewGuid());
}
