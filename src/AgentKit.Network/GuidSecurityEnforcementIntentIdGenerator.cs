// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Creates collision-resistant identities for independent network-enforcement attempts.</summary>
/// <remarks>The generator is singleton-safe and replaceable so hosts can provide deterministic identities for replay and testing.</remarks>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <inheritdoc/>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
