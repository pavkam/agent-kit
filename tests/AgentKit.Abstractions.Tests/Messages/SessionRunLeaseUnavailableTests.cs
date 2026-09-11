// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;
/// <summary>Verifies SessionRunLeaseUnavailable behavior and contracts.</summary>
public sealed class SessionRunLeaseUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SessionRunLeaseUnavailable>
{
    /// <inheritdoc/>
    protected override SessionRunLeaseUnavailable Create(string message) => new(message);
    /// <inheritdoc/>
    protected override string GetValue(SessionRunLeaseUnavailable subject) => subject.SafeReason;
    [Fact]
    public void LeaseOutcomeConstructors_WhenArgumentsAreInvalid_ThrowExactExceptions() => Should.Throw<ArgumentException>(() => new SessionRunLeaseUnavailable(" ")).ParamName.ShouldBe("safeReason");
}
