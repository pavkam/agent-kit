// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Creates collision-resistant security-request identities for protected-operation callers.</summary>
internal sealed class GuidSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    /// <inheritdoc/>
    public SecurityRequestId Create() => new(Guid.NewGuid());
}
