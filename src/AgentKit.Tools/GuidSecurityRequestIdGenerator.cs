// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Creates collision-resistant security-request identities for the tool executor pipeline.</summary>
internal sealed class GuidSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    /// <inheritdoc/>
    public SecurityRequestId Create() => new(Guid.NewGuid());
}
