// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies argument validation and structural equality of host-owned audit sink bindings.</summary>
public sealed class SecurityAuditSinkBindingTests
{
    [Fact]
    public void Constructor_WhenRegistrationOrSinkIsNull_ThrowsWithExactParameterNames()
    {
        var registration = new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], SecurityAuditDelivery.BestEffort, providesDurableAcceptance: false);
        var sink = new NoOpSink();
        var invalidRegistration = Should.Throw<ArgumentNullException>(() => new SecurityAuditSinkBinding(null!, sink));
        var invalidSink = Should.Throw<ArgumentNullException>(() => new SecurityAuditSinkBinding(registration, null!));
        invalidRegistration.ParamName.ShouldBe("registration");
        invalidSink.ParamName.ShouldBe("sink");
    }

    [Fact]
    public void Equals_WhenRegistrationAndSinkMatch_AreStructurallyEqual()
    {
        var registration = new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], SecurityAuditDelivery.BestEffort, providesDurableAcceptance: false);
        var sink = new NoOpSink();
        var first = new SecurityAuditSinkBinding(registration, sink);
        var second = new SecurityAuditSinkBinding(registration, sink);
        var different = new SecurityAuditSinkBinding(
            new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], SecurityAuditDelivery.Required, providesDurableAcceptance: true),
            sink);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(different);
    }

    [Fact]
    public void With_WhenCopyingWithoutChanges_RetainsTheOriginalRegistrationAndSink()
    {
        var registration = new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], SecurityAuditDelivery.BestEffort, providesDurableAcceptance: false);
        var sink = new NoOpSink();
        var original = new SecurityAuditSinkBinding(registration, sink);

        var copy = original with { };

        copy.Registration.ShouldBeSameAs(registration);
        copy.Sink.ShouldBeSameAs(sink);
        copy.ShouldNotBeSameAs(original);
    }

    private sealed class NoOpSink: ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
