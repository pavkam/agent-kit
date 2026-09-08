// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Creates unique enforcement-intent identities for default in-memory artifact-store operations.</summary>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <summary>Creates a fresh identity that correlates exactly one artifact-store operation.</summary>
    /// <returns>A non-default identity whose value contains no artifact content or metadata.</returns>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
