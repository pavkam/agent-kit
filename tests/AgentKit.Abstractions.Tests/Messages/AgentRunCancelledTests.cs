// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class AgentRunCancelledTests: Conformance.SingleMessageLeafConformanceTests<AgentRunCancelled>
{

    /// <inheritdoc/>
    protected override AgentRunCancelled Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(AgentRunCancelled subject) => subject.SafeMessage;
}
