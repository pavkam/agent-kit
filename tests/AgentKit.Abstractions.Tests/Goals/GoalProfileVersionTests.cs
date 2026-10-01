// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalProfileVersion behavior and contracts.</summary>
public sealed class GoalProfileVersionTests: Conformance.LongIdentityConformanceTests<GoalProfileVersion>
{
    /// <inheritdoc/>
    protected override GoalProfileVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(GoalProfileVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => false;
}
