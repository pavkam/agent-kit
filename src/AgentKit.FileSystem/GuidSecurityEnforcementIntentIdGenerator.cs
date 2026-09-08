// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Creates unique enforcement-intent identities for default filesystem effect attempts.</summary>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <summary>Creates one fresh identity that correlates exactly one filesystem enforcement attempt.</summary>
    /// <returns>A non-default identifier whose value is independent of path or content.</returns>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
