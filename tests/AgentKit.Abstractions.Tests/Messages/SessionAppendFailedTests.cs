// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionAppendFailedTests: Conformance.SingleMessageLeafConformanceTests<SessionAppendFailed>
{

    /// <inheritdoc/>
    protected override SessionAppendFailed Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionAppendFailed subject) => subject.SafeMessage;
}
