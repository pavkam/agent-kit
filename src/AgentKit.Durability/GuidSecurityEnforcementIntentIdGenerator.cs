// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Creates enforcement-intent identities from cryptographically random GUIDs.</summary>
/// <remarks>
/// Each identity names one exact grant-consumption attempt, so the grant store can reconcile a retried consumption
/// instead of spending a second use. This is the replaceable default.
/// </remarks>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <inheritdoc/>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
