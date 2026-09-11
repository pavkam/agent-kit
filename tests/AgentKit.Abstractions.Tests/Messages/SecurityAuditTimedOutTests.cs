// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;
/// <summary>Verifies SecurityAuditTimedOut behavior and contracts.</summary>
public sealed class SecurityAuditTimedOutTests: Conformance.SingleMessageLeafConformanceTests<SecurityAuditTimedOut>
{
    /// <inheritdoc/>
    protected override SecurityAuditTimedOut Create(string message) => new(message);
    /// <inheritdoc/>
    protected override string GetValue(SecurityAuditTimedOut subject) => subject.SafeReason;
    [Fact]
    public void AuditResultAndRedactedValueFactories_WhenArgumentsAreInvalid_ThrowWithExactParameterNames()
    {
        Should.Throw<ArgumentNullException>(() => new SecurityAuditTimedOut(null!)).ParamName.ShouldBe("safeReason");
        Should.Throw<ArgumentException>(() => new SecurityAuditTimedOut(" ")).ParamName.ShouldBe("safeReason");
    }
}
