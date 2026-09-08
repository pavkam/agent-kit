// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>Creates collision-resistant identities for independent scripted network-enforcement attempts.</summary>
/// <remarks>The generator is singleton-safe and replaceable so deterministic test hosts can control intent identities.</remarks>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <inheritdoc/>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
