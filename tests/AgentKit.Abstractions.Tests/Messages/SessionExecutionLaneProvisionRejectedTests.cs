// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionExecutionLaneProvisionRejectedTests: Conformance.SingleMessageLeafConformanceTests<SessionExecutionLaneProvisionRejected>
{

    /// <inheritdoc/>
    protected override SessionExecutionLaneProvisionRejected Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionExecutionLaneProvisionRejected subject) => subject.SafeMessage;
}
