// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

using AgentKit;

using Microsoft.Extensions.DependencyInjection;

public sealed class ServiceExtensionsTests
{
    private readonly record struct SampleId
    {
        public SampleId(Guid value) => Value = value;

        public Guid Value { get; }
    }

    private sealed class FirstGenerator: IIdentifierGenerator<SampleId>
    {
        public SampleId Create() => new(Guid.NewGuid());
    }

    private sealed class SecondGenerator: IIdentifierGenerator<SampleId>
    {
        public SampleId Create() => new(Guid.NewGuid());
    }

    private interface ISampleService;

    private sealed class SampleService: ISampleService;

    [Fact]
    public void TryAddIdentifierGenerator_WhenNoneRegistered_RegistersSingleton()
    {
        var services = new ServiceCollection();

        _ = services.TryAddIdentifierGenerator<SampleId, FirstGenerator>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IIdentifierGenerator<SampleId>>().ShouldBeOfType<FirstGenerator>();
    }

    [Fact]
    public void TryAddIdentifierGenerator_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.TryAddIdentifierGenerator<SampleId, FirstGenerator>();
        _ = services.TryAddIdentifierGenerator<SampleId, SecondGenerator>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IIdentifierGenerator<SampleId>>().ShouldBeOfType<FirstGenerator>();
    }

    [Fact]
    public void TryAddIdentifierGenerator_WhenCalled_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var result = services.TryAddIdentifierGenerator<SampleId, FirstGenerator>();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void DeclareAgentKitComponent_WhenServicesIsNull_ThrowsExactArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.DeclareAgentKitComponent(Registration()));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void DeclareAgentKitComponent_WhenRegistrationIsNull_ThrowsBeforeMutation()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentNullException>(() => services.DeclareAgentKitComponent(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("registration");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void DeclareAgentKitComponent_WhenCalled_AddsMetadataWithoutResolvingOrReplacingDuplicates()
    {
        var factoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISampleService>(
            _ =>
            {
                factoryCalls++;
                return new SampleService();
            });
        var registration = Registration();

        var result = services
            .DeclareAgentKitComponent(registration)
            .DeclareAgentKitComponent(registration);

        result.ShouldBeSameAs(services);
        services.Count(descriptor => descriptor.ServiceType == typeof(ComponentRegistrationDescriptor)).ShouldBe(2);
        factoryCalls.ShouldBe(0);
    }

    private static ComponentRegistrationDescriptor Registration() => new(
        ComponentContractReference.Unkeyed<ISampleService>(),
        typeof(SampleService),
        ServiceLifetime.Singleton,
        []);
}
