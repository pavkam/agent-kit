// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;
/// <summary>Verifies SecurityAuditFailed behavior and contracts.</summary>
public sealed class SecurityAuditFailedTests: Conformance.SingleMessageLeafConformanceTests<SecurityAuditFailed>
{
    /// <inheritdoc/>
    protected override SecurityAuditFailed Create(string message) => new(message);
    /// <inheritdoc/>
    protected override string GetValue(SecurityAuditFailed subject) => subject.SafeReason;
    [Fact]
    public void AuditResultAndRedactedValueFactories_WhenArgumentsAreInvalid_ThrowWithExactParameterNames()
    {
        Should.Throw<ArgumentNullException>(() => new SecurityAuditFailed(null!)).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentException>(() => new SecurityAuditFailed(" ")).ParamName.ShouldBe("safeReason");
    }
}
