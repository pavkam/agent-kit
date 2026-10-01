// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Creates unique enforcement-intent identities for default artifact-store operations.</summary>
internal sealed class GuidEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <summary>Creates a fresh identity that correlates exactly one artifact-store operation.</summary>
    /// <returns>A non-default identity whose value contains no artifact content or metadata.</returns>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
