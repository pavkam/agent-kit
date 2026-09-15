// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class AgentRunAuthorizationUnavailableTests: Conformance.SingleMessageLeafConformanceTests<AgentRunAuthorizationUnavailable>
{

    /// <inheritdoc/>
    protected override AgentRunAuthorizationUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(AgentRunAuthorizationUnavailable subject) => subject.SafeReason;
}
