// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json;

/// <summary>Creates distinct process-local enforcement-intent identities for goal-store operations.</summary>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <summary>Creates a non-default identity distinct from every other process-local operation.</summary>
    /// <returns>A new enforcement-intent identity.</returns>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
