// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class InvalidModelPolicyTests: Conformance.SingleMessageLeafConformanceTests<InvalidModelPolicy>
{

    /// <inheritdoc/>
    protected override InvalidModelPolicy Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(InvalidModelPolicy subject) => subject.Reason;
}
