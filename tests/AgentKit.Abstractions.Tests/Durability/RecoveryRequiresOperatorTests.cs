// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryRequiresOperator behavior and contracts.</summary>
public sealed class RecoveryRequiresOperatorTests: Conformance.SingleMessageLeafConformanceTests<RecoveryRequiresOperator>
{
    [Fact]
    public void RecoveryRequiresOperator_Constructor_WhenReasonIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryRequiresOperator(" "));
        exception.ParamName.ShouldBe("safeReason");
    }

    /// <inheritdoc/>
    protected override RecoveryRequiresOperator Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(RecoveryRequiresOperator subject) => subject.SafeReason;
}
