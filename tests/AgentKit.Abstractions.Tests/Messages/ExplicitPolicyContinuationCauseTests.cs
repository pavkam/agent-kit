// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class ExplicitPolicyContinuationCauseTests: Conformance.SingleMessageLeafConformanceTests<ExplicitPolicyContinuationCause>
{

    /// <inheritdoc/>
    protected override ExplicitPolicyContinuationCause Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(ExplicitPolicyContinuationCause subject) => subject.ReasonCode;
}
