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

    [Fact]
    public void With_WhenSafeReasonIsWhitespace_ThrowsArgumentException()
    {
        var requiresOperator = new RecoveryRequiresOperator("reason");
        Should.Throw<ArgumentException>(() => _ = requiresOperator with { SafeReason = " " }).ParamName.ShouldBe("SafeReason");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RecoveryRequiresOperator("reason");
        var copy = original with { };
        copy.ShouldBe(original);
        RecoveryDecision typed = original;
        _ = typed.ShouldBeOfType<RecoveryRequiresOperator>();
    }

    [Fact]
    public void With_WhenSafeReasonIsValid_UpdatesSafeReason()
    {
        var original = new RecoveryRequiresOperator("reason");
        var updated = original with { SafeReason = "other reason" };
        updated.SafeReason.ShouldBe("other reason");
    }
}
