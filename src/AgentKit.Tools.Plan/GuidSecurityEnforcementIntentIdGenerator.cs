// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Creates collision-resistant identities for independent plan-enforcement attempts.</summary>
/// <remarks>The generator is singleton-safe and replaceable so deterministic hosts can supply replay-stable identities.</remarks>
internal sealed class GuidSecurityEnforcementIntentIdGenerator:
    IIdentifierGenerator<SecurityEnforcementIntentId>
{
    /// <inheritdoc/>
    public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
}
