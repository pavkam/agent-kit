// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Generates unpredictable plan identities for the default registration.</summary>
internal sealed class GuidPlanIdGenerator: IIdentifierGenerator<PlanId>
{
    /// <inheritdoc/>
    public PlanId Create() => new(Guid.NewGuid());
}
