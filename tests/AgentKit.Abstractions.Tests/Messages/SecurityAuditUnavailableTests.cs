// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;
/// <summary>Verifies SecurityAuditUnavailable behavior and contracts.</summary>
public sealed class SecurityAuditUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SecurityAuditUnavailable>
{
    /// <inheritdoc/>
    protected override SecurityAuditUnavailable Create(string message) => new(message);
    /// <inheritdoc/>
    protected override string GetValue(SecurityAuditUnavailable subject) => subject.SafeReason;
    [Fact]
    public void AuditResultAndRedactedValueFactories_WhenArgumentsAreInvalid_ThrowWithExactParameterNames()
    {
        Should.Throw<ArgumentNullException>(() => new SecurityAuditUnavailable(null!)).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentException>(() => new SecurityAuditUnavailable(" ")).ParamName.ShouldBe("safeReason");
    }
}
