// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class GoalIdTests: Conformance.GuidIdentityConformanceTests<GoalId>
{

    /// <inheritdoc/>
    protected override GoalId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(GoalId subject) => subject.Value;
}
