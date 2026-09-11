// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class GoalAttemptIdTests: Conformance.GuidIdentityConformanceTests<GoalAttemptId>
{

    /// <inheritdoc/>
    protected override GoalAttemptId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(GoalAttemptId subject) => subject.Value;
}
