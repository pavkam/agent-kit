// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class ComponentRegistrationSnapshotTests
{
    [Fact]
    public void Constructor_WhenServicesAreDefault_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new ComponentRegistrationSnapshot(default, []));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Constructor_WhenRegistrationsContainNull_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new ComponentRegistrationSnapshot([], [null!]));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("registrations");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_WhenInfrastructureBoundIsInvalid_ThrowsExactArgumentOutOfRangeException(int value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ComponentRegistrationSnapshot([], [], value));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("maximumDerivedInfrastructureRegistrations");
    }

    [Fact]
    public void Capture_WhenServicesIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            ComponentRegistrationSnapshot.Capture(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("services");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Capture_WhenInfrastructureBoundIsInvalid_ThrowsBeforeDescriptorValidation(int value)
    {
        IServiceCollection services = new NullContainingServiceCollection { null! };

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            ComponentRegistrationSnapshot.Capture(services, value));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("maximumDerivedInfrastructureRegistrations");
    }

    [Fact]
    public void Capture_WhenServicesContainNull_ThrowsExactArgumentExceptionBeforeFactoryEffects()
    {
        var factoryCalls = 0;
        IServiceCollection services = new NullContainingServiceCollection
        {
            ServiceDescriptor.Singleton<ILeaf>(
                _ =>
                {
                    factoryCalls++;
                    return new Leaf();
                }),
            null!,
        };

        var exception = Should.Throw<ArgumentException>(() =>
            ComponentRegistrationSnapshot.Capture(services));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("services");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Capture_WhenCollectionChangesLater_RetainsTheOriginalBuildLocalEvidence()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILeaf, Leaf>();
        _ = services.DeclareAgentKitComponent(Registration(ServiceLifetime.Singleton));
        var snapshot = ComponentRegistrationSnapshot.Capture(services);

        _ = services.AddScoped<ILeaf, Leaf>();
        _ = services.DeclareAgentKitComponent(Registration(ServiceLifetime.Scoped));

        snapshot.Registrations.Length.ShouldBe(1);
        snapshot.Services.Count(static service => service.ServiceType == typeof(ILeaf)).ShouldBe(1);
        snapshot.Registrations.ShouldAllBe(static registration => registration.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Capture_WhenNoMetadataExists_ReportsHonestPartialCoverage()
    {
        var snapshot = ComponentRegistrationSnapshot.Capture(new ServiceCollection());

        snapshot.RepresentsCompleteRunnableGraph.ShouldBeFalse();
        snapshot.UnrepresentedRequiredSpine.ShouldContain(
            ComponentContractReference.Unkeyed<IAgentLoop>());
        snapshot.UnrepresentedRequiredSpine.ShouldContain(
            ComponentContractReference.Unkeyed<ISecurityGrantStore>());
        snapshot.Registrations.ShouldBeEmpty();
    }

    [Fact]
    public void Equality_WhenComparedWithItself_ReportsEqualAndPreservesRecordContract()
    {
        var snapshot = new ComponentRegistrationSnapshot([], []);

        snapshot.Equals(snapshot).ShouldBeTrue();
        snapshot.GetHashCode().ShouldBe(snapshot.GetHashCode());
        (snapshot with { }).Services.ShouldBe(snapshot.Services);
        snapshot.ToString().ShouldContain(nameof(ComponentRegistrationSnapshot));
    }

    private static ComponentRegistrationDescriptor Registration(ServiceLifetime lifetime) => new(
        ComponentContractReference.Unkeyed<ILeaf>(), typeof(Leaf), lifetime, []);

    private interface ILeaf;
    private sealed class Leaf: ILeaf;
    private sealed class NullContainingServiceCollection: List<ServiceDescriptor>, IServiceCollection;

}
