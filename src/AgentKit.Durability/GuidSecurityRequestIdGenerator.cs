// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Creates security-request identities from cryptographically random GUIDs.</summary>
/// <remarks>
/// This is the replaceable default used when no permissions package has already supplied one. Request identity
/// correlates one authorization attempt; it never carries authority.
/// </remarks>
internal sealed class GuidSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    /// <inheritdoc/>
    public SecurityRequestId Create() => new(Guid.NewGuid());
}
