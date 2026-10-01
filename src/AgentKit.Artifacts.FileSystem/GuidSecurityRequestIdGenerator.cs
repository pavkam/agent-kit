// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Creates unique security-request identities for default file-system artifact stores.</summary>
internal sealed class GuidSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    /// <summary>Creates a fresh identity that correlates exactly one file-effect authorization.</summary>
    /// <returns>A non-default identity.</returns>
    public SecurityRequestId Create() => new(Guid.NewGuid());
}
