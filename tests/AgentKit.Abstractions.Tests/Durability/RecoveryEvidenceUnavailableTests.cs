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
}
