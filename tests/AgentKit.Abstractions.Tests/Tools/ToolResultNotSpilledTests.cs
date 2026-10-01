// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies <see cref="ToolResultNotSpilled"/> requires a bounded reason.</summary>
public sealed class ToolResultNotSpilledTests: Conformance.SingleMessageLeafConformanceTests<ToolResultNotSpilled>
{
    /// <inheritdoc/>
    protected override ToolResultNotSpilled Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(ToolResultNotSpilled subject) => subject.SafeReason;
}
