// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class ProcessOutputArtifactRejectedTests: Conformance.SingleMessageLeafConformanceTests<ProcessOutputArtifactRejected>
{

    /// <inheritdoc/>
    protected override ProcessOutputArtifactRejected Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(ProcessOutputArtifactRejected subject) => subject.SafeMessage;
}
