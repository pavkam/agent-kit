// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryEvidenceUnavailable behavior and contracts.</summary>
public sealed class RecoveryEvidenceUnavailableTests: Conformance.SingleMessageLeafConformanceTests<RecoveryEvidenceUnavailable>
{
    [Fact]
    public void RecoveryEvidenceUnavailable_Constructor_WhenMessageIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryEvidenceUnavailable(string.Empty));
        exception.ParamName.ShouldBe("safeMessage");
    }

    /// <inheritdoc/>
    protected override RecoveryEvidenceUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(RecoveryEvidenceUnavailable subject) => subject.SafeMessage;

    [Fact]
    public void With_WhenSafeMessageIsWhitespace_ThrowsArgumentException()
    {
        var unavailable = new RecoveryEvidenceUnavailable("message");
        Should.Throw<ArgumentException>(() => _ = unavailable with { SafeMessage = " " }).ParamName.ShouldBe("SafeMessage");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RecoveryEvidenceUnavailable("message");
        var copy = original with { };
        copy.ShouldBe(original);
        RecoveryEvidenceResult result = original;
        _ = result.ShouldBeOfType<RecoveryEvidenceUnavailable>();
    }

    [Fact]
    public void With_WhenSafeMessageIsValid_UpdatesSafeMessage()
    {
        var original = new RecoveryEvidenceUnavailable("message");
        var updated = original with { SafeMessage = "other message" };
        updated.SafeMessage.ShouldBe("other message");
    }
}
