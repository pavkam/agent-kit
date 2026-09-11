// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SessionLoadFailedTests: Conformance.SingleMessageLeafConformanceTests<SessionLoadFailed>
{

    /// <inheritdoc/>
    protected override SessionLoadFailed Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SessionLoadFailed subject) => subject.SafeMessage;
}
