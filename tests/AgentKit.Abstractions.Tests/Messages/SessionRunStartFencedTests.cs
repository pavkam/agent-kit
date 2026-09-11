// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionRunStartFencedTests: Conformance.SingleMessageLeafConformanceTests<SessionRunStartFenced>
{

    /// <inheritdoc/>
    protected override SessionRunStartFenced Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionRunStartFenced subject) => subject.SafeReason;
}
