// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class OutputRepairInstructionTests: Conformance.SingleMessageLeafConformanceTests<OutputRepairInstruction>
{

    /// <inheritdoc/>
    protected override OutputRepairInstruction Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(OutputRepairInstruction subject) => subject.SafeMessage;
}
