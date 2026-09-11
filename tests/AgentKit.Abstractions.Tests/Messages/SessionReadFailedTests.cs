// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionReadFailedTests: Conformance.SingleMessageLeafConformanceTests<SessionReadFailed>
{

    /// <inheritdoc/>
    protected override SessionReadFailed Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionReadFailed subject) => subject.SafeMessage;
}
