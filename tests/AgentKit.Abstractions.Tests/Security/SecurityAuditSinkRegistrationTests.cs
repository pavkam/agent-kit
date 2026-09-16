// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityAuditSinkRegistration behavior and contracts.</summary>
public sealed class SecurityAuditSinkRegistrationTests
{
    [Fact]
    public void Constructor_WhenAuditSinkRegistrationHasInvalidSupportOrDelivery_ThrowsForSupportedEventKindsOrDelivery()
    {
        Should.Throw<ArgumentException>(() => new SecurityAuditSinkRegistration(default, SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentException>(() => new SecurityAuditSinkRegistration([], SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuditSinkRegistration([(SecurityAuditEventKind) 99], SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentException>(() => new SecurityAuditSinkRegistration([SecurityAuditEventKind.Decision, SecurityAuditEventKind.Decision], SecurityAuditDelivery.Required, true)).ParamName.ShouldBe("supportedEventKinds");
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAuditSinkRegistration([SecurityAuditEventKind.Decision], (SecurityAuditDelivery) 99, true)).ParamName.ShouldBe("delivery");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var registration = new SecurityAuditSinkRegistration([SecurityAuditEventKind.Decision], SecurityAuditDelivery.Required, true);
        registration.SupportedEventKinds.ShouldBe([SecurityAuditEventKind.Decision]);
        registration.Delivery.ShouldBe(SecurityAuditDelivery.Required);
        registration.ProvidesDurableAcceptance.ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuditSinkRegistration([SecurityAuditEventKind.Decision], SecurityAuditDelivery.Required, true);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
