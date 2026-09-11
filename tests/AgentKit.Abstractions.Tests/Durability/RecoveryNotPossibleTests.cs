// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryNotPossible behavior and contracts.</summary>
public sealed class RecoveryNotPossibleTests: Conformance.SingleMessageLeafConformanceTests<RecoveryNotPossible>
{
    [Fact]
    public void RecoveryNotPossible_Constructor_WhenReasonIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryNotPossible(null!));
        exception.ParamName.ShouldBe("safeReason");
    }

    /// <inheritdoc/>
    protected override RecoveryNotPossible Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(RecoveryNotPossible subject) => subject.SafeReason;
}
