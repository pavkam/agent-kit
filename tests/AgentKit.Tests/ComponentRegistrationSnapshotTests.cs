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
    public void Capture_WhenRunnableCompositionPublishesNoDescriptors_ReportsEveryRegisteredSelectableComponentAsUnrepresented()
    {
        var builder = CompositionTestData.RunnableBuilder();

        var snapshot = ComponentRegistrationSnapshot.Capture(builder.Services);

        snapshot.RepresentsCompleteRunnableGraph.ShouldBeFalse();
        snapshot.UnrepresentedRequiredSpine.ShouldContain(new ComponentContractReference(typeof(IAgentLoop), AgentLoopComponentDefaults.LoopKeyValue));
        snapshot.UnrepresentedRequiredSpine.ShouldContain(new ComponentContractReference(typeof(IModelSelector), AgentProviderComponentDefaults.ModelSelectorKeyValue));
        snapshot.UnrepresentedRequiredSpine.ShouldContain(new ComponentContractReference(typeof(IOutputProcessor), AgentOutputComponentDefaults.ProcessorKeyValue));
    }

    [Fact]
    public void Capture_WhenEveryRequiredAndRegisteredSelectableComponentIsDeclared_RepresentsTheCompleteRunnableGraph()
    {
        var builder = CompositionTestData.RunnableBuilder();
        var unrepresented = ComponentRegistrationSnapshot.Capture(builder.Services).UnrepresentedRequiredSpine;
        using var provider = builder.Services.BuildServiceProvider();
        foreach (var reference in unrepresented)
        {
            _ = builder.Services.DeclareAgentKitComponent(Declaration(provider, builder.Services, reference));
        }

        var snapshot = ComponentRegistrationSnapshot.Capture(builder.Services);

        snapshot.UnrepresentedRequiredSpine.ShouldBeEmpty();
        snapshot.RepresentsCompleteRunnableGraph.ShouldBeTrue();
    }

    [Fact]
    public void RepresentsCompleteRunnableGraph_WhenOneRegisteredKeyedComponentIsUndeclared_IsFalse()
    {
        var builder = CompositionTestData.RunnableBuilder();
        var unrepresented = ComponentRegistrationSnapshot.Capture(builder.Services).UnrepresentedRequiredSpine;
        var omitted = new ComponentContractReference(typeof(IInputCoordinator), AgentIOComponentDefaults.InputCoordinatorKeyValue);
        using var provider = builder.Services.BuildServiceProvider();
        foreach (var reference in unrepresented.Where(reference => reference != omitted))
        {
            _ = builder.Services.DeclareAgentKitComponent(Declaration(provider, builder.Services, reference));
        }

        var snapshot = ComponentRegistrationSnapshot.Capture(builder.Services);

        snapshot.RepresentsCompleteRunnableGraph.ShouldBeFalse();
        snapshot.UnrepresentedRequiredSpine.ShouldBe([omitted]);
    }

    /// <summary>Declares one registered address with the concrete implementation type it actually produces.</summary>
    private static ComponentRegistrationDescriptor Declaration(IServiceProvider provider, IServiceCollection services, ComponentContractReference reference)
    {
        var descriptor = services.Last(candidate =>
            candidate.ServiceType == reference.ContractType
            && (reference.Key is null ? !candidate.IsKeyedService : Equals(candidate.ServiceKey, reference.Key)));
        // An opaque factory exposes no implementation type, so the test observes what the registration really builds.
        var implementation = (reference.Key is null
            ? provider.GetRequiredService(reference.ContractType)
            : ((IKeyedServiceProvider) provider).GetRequiredKeyedService(reference.ContractType, reference.Key)).GetType();
        return new ComponentRegistrationDescriptor(reference, implementation, descriptor.Lifetime, []);
    }

    [Fact]
    public void Capture_WhenNoMetadataExists_ReportsHonestPartialCoverage()
    {
        var snapshot = ComponentRegistrationSnapshot.Capture(new ServiceCollection());

        snapshot.RepresentsCompleteRunnableGraph.ShouldBeFalse();
        snapshot.UnrepresentedRequiredSpine.ShouldContain(
            ComponentContractReference.Unkeyed<IAgentDefinitionCatalog>());
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
