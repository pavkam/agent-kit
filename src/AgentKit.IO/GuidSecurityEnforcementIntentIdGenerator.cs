// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Creates distinct local enforcement-intent identities for human-question publication attempts.</summary>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <summary>Creates a non-default identity that distinguishes one publication attempt from every other process-local attempt.</summary>
    /// <returns>A new non-default enforcement-intent identity.</returns>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
