// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies sink registration invariants and profile filtering.</summary>
public sealed class DurableExecutionEventSinkRegistrationTests
{
    private static readonly DurabilityProfileKey Primary = new("primary");
    private static readonly DurabilityProfileKey Secondary = new("secondary");

    [Fact]
    public void Constructor_WhenTheIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableExecutionEventSinkRegistration(
                default,
                order: 0,
                DurableExecutionEventDelivery.Observational,
                ServiceLifetime.Singleton));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenTheDeliveryIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableExecutionEventSinkRegistration(
                new DurableExecutionEventSinkId("audit"),
                order: 0,
                (DurableExecutionEventDelivery) 99,
                ServiceLifetime.Singleton));

        exception.ParamName.ShouldBe("delivery");
    }

    [Fact]
    public void Constructor_WhenTheLifetimeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new DurableExecutionEventSinkRegistration(
                new DurableExecutionEventSinkId("audit"),
                order: 0,
                DurableExecutionEventDelivery.Observational,
                (ServiceLifetime) 99));

        exception.ParamName.ShouldBe("lifetime");
    }

    [Fact]
    public void Constructor_WhenAProfileIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => Register([Primary, default]));

        exception.ParamName.ShouldBe("profiles");
    }

    [Fact]
    public void Constructor_WhenAProfileIsDuplicated_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => Register([Primary, Primary]));

        exception.ParamName.ShouldBe("profiles");
    }

    [Fact]
    public void Constructor_WhenProfilesAreOmitted_TreatsTheFilterAsEmpty()
    {
        var registration = Register();

        registration.Profiles.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenTheOrderIsNegative_IsAccepted()
    {
        // Negative order is how a sink declares that it runs ahead of the defaults.
        var registration = new DurableExecutionEventSinkRegistration(
            new DurableExecutionEventSinkId("audit"),
            order: -10,
            DurableExecutionEventDelivery.Required,
            ServiceLifetime.Scoped);

        registration.Order.ShouldBe(-10);
        registration.Delivery.ShouldBe(DurableExecutionEventDelivery.Required);
        registration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Observes_WhenTheFilterIsEmpty_ObservesEveryProfile()
    {
        var registration = Register();

        registration.Observes(Primary).ShouldBeTrue();
        registration.Observes(Secondary).ShouldBeTrue();
    }

    [Fact]
    public void Observes_WhenTheFilterNamesTheProfile_ReturnsTrue()
    {
        var registration = Register([Primary]);

        registration.Observes(Primary).ShouldBeTrue();
    }

    [Fact]
    public void Observes_WhenTheFilterOmitsTheProfile_ReturnsFalse()
    {
        var registration = Register([Primary]);

        registration.Observes(Secondary).ShouldBeFalse();
    }

    private static DurableExecutionEventSinkRegistration Register(
        ImmutableArray<DurabilityProfileKey> profiles = default) =>
        new(
            new DurableExecutionEventSinkId("audit"),
            order: 0,
            DurableExecutionEventDelivery.Observational,
            ServiceLifetime.Singleton,
            profiles);
}
