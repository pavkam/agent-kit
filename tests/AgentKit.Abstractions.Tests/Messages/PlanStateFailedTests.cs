// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class PlanStateFailedTests: Conformance.SingleMessageLeafConformanceTests<PlanStateFailed>
{

    /// <inheritdoc/>
    protected override PlanStateFailed Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(PlanStateFailed subject) => subject.SafeMessage;
}
