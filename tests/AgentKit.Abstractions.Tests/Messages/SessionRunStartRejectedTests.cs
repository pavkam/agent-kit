// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionRunStartRejectedTests: Conformance.SingleMessageLeafConformanceTests<SessionRunStartRejected>
{

    /// <inheritdoc/>
    protected override SessionRunStartRejected Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionRunStartRejected subject) => subject.SafeReason;
}
