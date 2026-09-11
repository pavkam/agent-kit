// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class PlanStateDeniedTests: Conformance.SingleMessageLeafConformanceTests<PlanStateDenied>
{

    /// <inheritdoc/>
    protected override PlanStateDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(PlanStateDenied subject) => subject.SafeMessage;
}
