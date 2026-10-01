// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Creates unpredictable security-request identities for the default goal registration.</summary>
internal sealed class GuidSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    /// <summary>Creates a new non-default security-request identity.</summary>
    /// <returns>A fresh identity.</returns>
    public SecurityRequestId Create() => new(Guid.NewGuid());
}
