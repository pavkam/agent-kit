// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Creates collision-resistant network operation identities.</summary>
internal sealed class GuidNetworkOperationIdGenerator: IIdentifierGenerator<NetworkOperationId>
{
    /// <inheritdoc/>
    public NetworkOperationId Create() => new(Guid.NewGuid());
}
