// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Creates collision-resistant grant identities for the process-local permission runtime.</summary>
internal sealed class GuidGrantIdGenerator: IIdentifierGenerator<GrantId>
{
    /// <inheritdoc/>
    public GrantId Create() => new(Guid.NewGuid());
}
