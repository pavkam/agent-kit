// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class PlanIdTests: Conformance.GuidIdentityConformanceTests<PlanId>
{

    /// <inheritdoc/>
    protected override PlanId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(PlanId subject) => subject.Value;
}
