// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionExecutionLaneProvisionConflictTests: Conformance.SingleMessageLeafConformanceTests<SessionExecutionLaneProvisionConflict>
{

    /// <inheritdoc/>
    protected override SessionExecutionLaneProvisionConflict Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionExecutionLaneProvisionConflict subject) => subject.SafeMessage;
}
