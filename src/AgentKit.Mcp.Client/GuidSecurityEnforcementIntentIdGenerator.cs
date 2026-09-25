// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Creates collision-resistant identities for MCP client grant-consumption intents.</summary>
internal sealed class GuidSecurityEnforcementIntentIdGenerator: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <inheritdoc/>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
