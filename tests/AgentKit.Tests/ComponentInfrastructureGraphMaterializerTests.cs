// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ComponentInfrastructureGraphMaterializerTests
{
    [Fact]
    public void Materialize_WhenSnapshotIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            ComponentInfrastructureGraphMaterializer.Materialize(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void Validate_WhenSnapshotIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            ComponentDependencyGraphValidator.ValidateSnapshot(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void Materialize_WhenRealAddLoggingIsSelected_ClosesThePinnedInfrastructureGraph()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var snapshot = Snapshot(services, RootRegistration(LoggerDependency()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations.ShouldContain(registration =>
            registration.Service.ContractType == typeof(ILogger<LoggerCategory>));
        Registrations.ShouldContain(registration =>
            registration.Service.ContractType == typeof(ILoggerFactory));
        Registrations.ShouldContain(registration =>
            registration.Service.ContractType == typeof(IOptions<LoggerFactoryOptions>));
        Registrations.ShouldContain(registration =>
            registration.Service.ContractType == typeof(IOptionsMonitor<LoggerFilterOptions>));
        var loggerFactory = Registrations.Single(registration =>
            registration.Service.ContractType == typeof(ILoggerFactory));
        loggerFactory.Dependencies.ShouldContain(dependency =>
            dependency.Reference.ContractType == typeof(ILoggerProvider)
            && dependency.Cardinality == ComponentDependencyCardinality.AdditiveCollection);
        ComponentDependencyGraphValidator.ValidateSnapshot(snapshot).ShouldBeEmpty();
    }

    [Fact]
    public void Materialize_WhenOptionalInfrastructureIsAbsent_DoesNotInventALeaf()
    {
        var snapshot = Snapshot(
            new ServiceCollection(),
            RootRegistration(InfrastructureDependency<IExternal>(ComponentDependencyCardinality.OptionalSingular)));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        _ = Registrations.ShouldHaveSingleItem();
        ComponentDependencyGraphValidator.ValidateSnapshot(snapshot).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPresentOptionalInfrastructureClosesCycle_ReportsCompleteCycle()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, ExternalBackEdge>();
        var snapshot = Snapshot(
            services,
            RootRegistration(InfrastructureDependency<IExternal>(ComponentDependencyCardinality.OptionalSingular)));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-dependency.cycle");
        var diagnostic = diagnostics.Single(static item => item.Code == "agentkit.component-dependency.cycle");
        diagnostic.SafeMessage.ShouldContain(typeof(IInfrastructureRoot).FullName!);
        diagnostic.SafeMessage.ShouldContain(typeof(IExternal).FullName!);
    }

    [Fact]
    public void Validate_WhenSingletonCapturesScopedInfrastructure_ReportsCaptivity()
    {
        var services = new ServiceCollection();
        _ = services.AddScoped<IExternal, External>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-lifetime.captive-scoped");
    }

    [Fact]
    public void Materialize_WhenInfrastructureUsesOpaqueFactory_RejectsWithoutInvokingFactory()
    {
        var factoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal>(
            _ =>
            {
                factoryCalls++;
                return new External();
            });
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.opaque-factory");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Validate_WhenOpaqueFactoryHasExactExplicitDeclaration_UsesDeclaredProofWithoutFactoryEffects()
    {
        var factoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton<IInfrastructureRoot, InfrastructureRoot>();
        _ = services.AddSingleton<IExternal>(
            _ =>
            {
                factoryCalls++;
                return new External();
            });
        var snapshot = Snapshot(
            services,
            RootRegistration(InfrastructureDependency<IExternal>()),
            Registration<IExternal, External>(ServiceLifetime.Singleton));

        AgentCompositionValidator.ValidateComponentRegistrations(snapshot);

        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Materialize_WhenExactClosedAndOpenGenericRegistrationsExist_SingularUsesClosedRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OpenExternal<>));
        _ = services.AddSingleton<IGenericExternal<string>, ClosedStringExternal>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IGenericExternal<string>>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        var matches = Registrations
            .Where(static registration =>
                registration.Service.ContractType == typeof(IGenericExternal<string>))
            .ToArray();
        matches.ShouldHaveSingleItem().ImplementationType.ShouldBe(typeof(ClosedStringExternal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_WhenSingularInfrastructureHasMultipleEffectiveRegistrations_ReportsAmbiguity(
        bool exactClosed)
    {
        var services = new ServiceCollection();
        if (exactClosed)
        {
            _ = services.AddSingleton<IGenericExternal<string>, ClosedStringExternal>();
            _ = services.AddSingleton<IGenericExternal<string>, OtherClosedStringExternal>();
        }
        else
        {
            _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OpenExternal<>));
            _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OtherOpenExternal<>));
        }

        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IGenericExternal<string>>()));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-dependency.ambiguous");
    }

    [Fact]
    public void Materialize_WhenCollectionMixesClosedAndOpenGenericRegistrations_PreservesDiUnionOrder()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OpenExternal<>));
        _ = services.AddSingleton<IGenericExternal<string>, ClosedStringExternal>();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OtherOpenExternal<>));
        var snapshot = Snapshot(
            services,
            RootRegistration(InfrastructureDependency<IGenericExternal<string>>(
                ComponentDependencyCardinality.AdditiveCollection)));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations
            .Where(static registration =>
                registration.Service.ContractType == typeof(IGenericExternal<string>))
            .Select(static registration => registration.ImplementationType)
            .ShouldBe([
                typeof(OpenExternal<string>),
                typeof(ClosedStringExternal),
                typeof(OtherOpenExternal<string>),
            ]);
    }

    [Fact]
    public void Validate_WhenSingularAndCollectionConsumeSameInfrastructure_PreservesEachResolutionRule()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OpenExternal<>));
        _ = services.AddSingleton<IGenericExternal<string>, ClosedStringExternal>();
        var snapshot = Snapshot(
            services,
            RootRegistration(InfrastructureDependency<IGenericExternal<string>>()),
            CollectionRootRegistration(InfrastructureDependency<IGenericExternal<string>>(
                ComponentDependencyCardinality.AdditiveCollection)));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenDeclaredOnlyAndInfrastructureEdgesShareAddress_DerivedNodeCannotSatisfyOptedOutEdge()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, External>();
        var snapshot = Snapshot(
            services,
            RootRegistration(InfrastructureDependency<IExternal>()),
            CollectionRootRegistration(new ComponentDependencyDescriptor(
                ComponentContractReference.Unkeyed<IExternal>(),
                ComponentDependencyCardinality.RequiredSingular)));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Count(static item => item.Code == "agentkit.component-dependency.missing")
            .ShouldBe(1);
        diagnostics.Single(static item => item.Code == "agentkit.component-dependency.missing")
            .SafeMessage.ShouldContain(typeof(ICollectionInfrastructureRoot).FullName!);
    }

    [Fact]
    public void Materialize_WhenCollectionHasExplicitClosedDeclaration_DoesNotHideOpenRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OpenExternal<>));
        _ = services.AddSingleton<IGenericExternal<string>, ClosedStringExternal>();
        var snapshot = Snapshot(
            services,
            CollectionRootRegistration(InfrastructureDependency<IGenericExternal<string>>(
                ComponentDependencyCardinality.AdditiveCollection)),
            Registration<IGenericExternal<string>, ClosedStringExternal>(ServiceLifetime.Singleton));

        var (Registrations, Diagnostics, DependencyTargets) =
            ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations.Length.ShouldBe(3);
        DependencyTargets[(0, 0)].ShouldBe([2, 1]);
        ComponentDependencyGraphValidator.ValidateSnapshot(snapshot).ShouldBeEmpty();
    }

    [Fact]
    public void Materialize_WhenCollectionMetadataOrderDiffersFromDiOrder_MapsEachExplicitRegistrationInDiOrder()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IGenericExternal<string>, ClosedStringExternal>();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(OpenExternal<>));
        _ = services.AddSingleton<IGenericExternal<string>, OtherClosedStringExternal>();
        var snapshot = Snapshot(
            services,
            CollectionRootRegistration(InfrastructureDependency<IGenericExternal<string>>(
                ComponentDependencyCardinality.AdditiveCollection)),
            Registration<IGenericExternal<string>, OtherClosedStringExternal>(ServiceLifetime.Singleton),
            Registration<IGenericExternal<string>, ClosedStringExternal>(ServiceLifetime.Singleton));

        var (_, Diagnostics, DependencyTargets) =
            ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        DependencyTargets[(0, 0)].ShouldBe([2, 3, 1]);
    }

    [Fact]
    public void Materialize_WhenGenericConstraintsCannotClose_ReportsInvalidGenericEvidence()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(StructOnlyExternal<>));
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IGenericExternal<string>>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.generic-closure-invalid");
    }

    [Fact]
    public void Materialize_WhenOpenGenericExpansionExceedsConfiguredBound_RejectsWithoutClaimingInfinity()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(INode<>), typeof(ExpandingNode<>));
        var snapshot = SnapshotWithMaximum(
            services,
            maximumDerivedInfrastructureRegistrations: 1,
            RootRegistration(InfrastructureDependency<INode<string>>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.validation-bound-exceeded");
        Registrations.Length.ShouldBe(2);
    }

    [Fact]
    public void Materialize_WhenExpandingOpenGenericReachesExactClosedTerminal_ClosesFiniteGraph()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(INode<>), typeof(ExpandingNode<>));
        _ = services.AddSingleton<INode<List<List<string>>>, TerminalNestedStringListNode>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<INode<string>>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations.ShouldContain(registration =>
            registration.Service.ContractType == typeof(INode<List<List<string>>>)
            && registration.ImplementationType == typeof(TerminalNestedStringListNode));
    }

    [Fact]
    public void Materialize_WhenConstructorsHaveIncompatibleResolvableDependencies_ReportsAmbiguity()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, AmbiguousExternal>();
        _ = services.AddSingleton<IFirstDependency>(new FirstDependency());
        _ = services.AddSingleton<ISecondDependency>(new SecondDependency());
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.constructor-ambiguous");
    }

    [Fact]
    public void Materialize_WhenNoConstructorDependenciesCanBeSupplied_ReportsUnresolvableConstructor()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, UnresolvableExternal>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.constructor-unresolvable");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Materialize_WhenSelectedConstructorUsesUnsupportedDiBehavior_RejectsIt(bool serviceLocator)
    {
        var services = new ServiceCollection();
        if (serviceLocator)
        {
            _ = services.AddSingleton<IExternal, ServiceLocatorExternal>();
        }
        else
        {
            _ = services.AddKeyedSingleton<IFirstDependency, FirstDependency>("first");
            _ = services.AddSingleton<IExternal, KeyedExternal>();
        }

        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
    }

    [Fact]
    public void Materialize_WhenDescriptorKeyEqualsThrows_DoesNotInvokeApplicationKeyCallbacks()
    {
        var key = new ThrowingKey();
        var services = new ServiceCollection
        {
            ServiceDescriptor.KeyedSingleton<IFirstDependency, FirstDependency>(key),
            ServiceDescriptor.Singleton<IExternal, KeyedExternal>(),
        };
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
        key.EqualsCalls.ShouldBe(0);
    }

    [Fact]
    public void Materialize_WhenAnyKeyDescriptorCouldAffectKeyedConstructor_FailsClosedWithoutMatchInference()
    {
        var services = new ServiceCollection
        {
            ServiceDescriptor.KeyedSingleton<IFirstDependency, FirstDependency>(KeyedService.AnyKey),
            ServiceDescriptor.Singleton<IExternal, KeyedExternal>(),
        };
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
    }

    [Fact]
    public void Materialize_WhenExplicitNullLookupHasOnlyKeyedDescriptor_DoesNotInventAvailability()
    {
        var services = new ServiceCollection
        {
            ServiceDescriptor.KeyedSingleton<IFirstDependency, FirstDependency>("first"),
            ServiceDescriptor.Singleton<IExternal, NullKeyFallbackExternal>(),
        };
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations.Single(registration => registration.Service.ContractType == typeof(IExternal))
            .Dependencies.ShouldBeEmpty();
    }

    [Fact]
    public void Materialize_WhenStringKeyedCollectionIsEmpty_RejectsSelectedUnsupportedConstructor()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, StringKeyedCollectionExternal>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
        StringKeyedCollectionExternal.ActivationCount.ShouldBe(0);
    }

    [Fact]
    public void Materialize_WhenNullKeyedCollectionIsEmpty_RejectsSelectedUnsupportedConstructor()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, NullKeyedCollectionExternal>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
        NullKeyedCollectionExternal.ActivationCount.ShouldBe(0);
    }

    [Fact]
    public void Validate_WhenConstructorDeferredLocatorHasExactDeclaration_ValidatesItsLifetimeWithoutFactoryEffects()
    {
        var factoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDeferredConsumer, DeferredConsumer>();
        _ = services.AddScoped<Func<IExternal>>(
            _ =>
            {
                factoryCalls++;
                return static () => new External();
            });
        var snapshot = Snapshot(
            services,
            RootRegistration(InfrastructureDependency<IDeferredConsumer>()),
            new ComponentRegistrationDescriptor(
                ComponentContractReference.Unkeyed<Func<IExternal>>(),
                typeof(Func<IExternal>),
                ServiceLifetime.Scoped,
                []));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Select(static item => item.Code)
            .ShouldNotContain("agentkit.component-infrastructure.dependency-unsupported");
        diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-lifetime.captive-scoped");
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Materialize_WhenDeferredLocatorIsRegisteredAsInstance_RejectsWithoutInvokingIt(bool lazy)
    {
        var invocationCount = 0;
        object deferred = lazy
            ? new Lazy<IExternal>(() =>
            {
                invocationCount++;
                return new External();
            })
            : new Func<IExternal>(() =>
            {
                invocationCount++;
                return new External();
            });
        var services = new ServiceCollection
        {
            ServiceDescriptor.Singleton(deferred.GetType(), deferred),
        };
        var snapshot = Snapshot(
            services,
            RootRegistration(new ComponentDependencyDescriptor(
                new ComponentContractReference(deferred.GetType()),
                ComponentDependencyCardinality.RequiredSingular,
                factoryBoundary: null,
                ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure)));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
        invocationCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_WhenDeferredLocatorHasExactOrdinaryDeclaration_AcceptsProofWithoutInvokingIt(bool lazy)
    {
        var invocationCount = 0;
        object deferred = lazy
            ? new Lazy<IExternal>(() =>
            {
                invocationCount++;
                return new External();
            })
            : new Func<IExternal>(() =>
            {
                invocationCount++;
                return new External();
            });
        var service = new ComponentContractReference(deferred.GetType());
        var services = new ServiceCollection
        {
            ServiceDescriptor.Singleton<IInfrastructureRoot, InfrastructureRoot>(),
            ServiceDescriptor.Singleton(deferred.GetType(), deferred),
        };
        var snapshot = Snapshot(
            services,
            RootRegistration(new ComponentDependencyDescriptor(
                service,
                ComponentDependencyCardinality.RequiredSingular,
                factoryBoundary: null,
                ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure)),
            new ComponentRegistrationDescriptor(
                service,
                deferred.GetType(),
                ServiceLifetime.Singleton,
                []));

        AgentCompositionValidator.ValidateComponentRegistrations(snapshot);

        invocationCount.ShouldBe(0);
    }

    [Fact]
    public void Materialize_WhenKeyCaseDiffers_DoesNotTreatKeyAsAMatch()
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<IExternal, External>("Alpha");
        var snapshot = Snapshot(
            services,
            RootRegistration(new ComponentDependencyDescriptor(
                new ComponentContractReference(typeof(IExternal), "alpha"),
                ComponentDependencyCardinality.RequiredSingular,
                factoryBoundary: null,
                ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure)));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-dependency.missing");
    }

    [Fact]
    public void Materialize_WhenOrdinalKeyMatches_ClosesTheKeyedRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<IExternal, External>("Alpha");
        var reference = new ComponentContractReference(typeof(IExternal), "Alpha");
        var snapshot = Snapshot(
            services,
            RootRegistration(new ComponentDependencyDescriptor(
                reference,
                ComponentDependencyCardinality.RequiredSingular,
                factoryBoundary: null,
                ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure)));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations.ShouldContain(registration =>
            registration.Service.Equals(reference)
            && registration.ImplementationType == typeof(External));
    }

    [Fact]
    public void Materialize_WhenInstanceZeroConstructorDefaultAndEmptyCollectionAreUsed_StopsOnlyAtProvenLeaves()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFirstDependency>(new FirstDependency());
        _ = services.AddSingleton<IExternal, DefaultAndCollectionExternal>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations.ShouldContain(registration =>
            registration.Service.ContractType == typeof(IFirstDependency)
            && registration.Dependencies.IsEmpty);
        var external = Registrations.Single(registration =>
            registration.Service.ContractType == typeof(IExternal));
        external.Dependencies.Length.ShouldBe(2);
        external.Dependencies.ShouldContain(dependency =>
            dependency.Cardinality == ComponentDependencyCardinality.AdditiveCollection
            && dependency.Reference.ContractType == typeof(ISecondDependency));
        external.Dependencies.ShouldContain(dependency =>
            dependency.Cardinality == ComponentDependencyCardinality.RequiredSingular
            && dependency.Reference.ContractType == typeof(IFirstDependency));
        external.Dependencies.ShouldNotContain(dependency =>
            dependency.Reference.ContractType == typeof(IOptionalDependency));
    }

    [Fact]
    public void Materialize_WhenPrimitiveEnumAndNullDefaultsAreAbsent_TreatsThemAsTerminalDefaults()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, PrimitiveDefaultsExternal>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.ShouldBeEmpty();
        Registrations.Single(registration => registration.Service.ContractType == typeof(IExternal))
            .Dependencies.ShouldBeEmpty();
    }

    [Fact]
    public void Materialize_WhenUnsupportedValueTypeDefaultHasPresentRegistration_FailsClosed()
    {
        var services = new ServiceCollection
        {
            ServiceDescriptor.Singleton<IExternal, PrimitiveDefaultsExternal>(),
            ServiceDescriptor.Singleton(typeof(int), 42),
        };
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
    }

    [Fact]
    public void Validate_WhenPresentDefaultReferenceIsScoped_PreservesOptionalEdgeCaptivity()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, OptionalReferenceExternal>();
        _ = services.AddScoped<IOptionalDependency, OptionalDependency>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-lifetime.captive-scoped");
    }

    [Fact]
    public void Validate_WhenPresentDefaultReferenceClosesCycle_PreservesOptionalEdgeCycle()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, OptionalBackEdge>();
        _ = services.AddSingleton<IInfrastructureRoot, InfrastructureRoot>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var diagnostics = ComponentDependencyGraphValidator.ValidateSnapshot(snapshot);

        diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-dependency.cycle");
    }

    [Fact]
    public void Materialize_WhenTwoOwnersDependOnTheSameUnresolvableInfrastructure_ReportsDiagnosticOnce()
    {
        var factoryCalls = 0;
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal>(
            _ =>
            {
                factoryCalls++;
                return new External();
            });
        var snapshot = Snapshot(
            services,
            RootRegistration(InfrastructureDependency<IExternal>()),
            CollectionRootRegistration(InfrastructureDependency<IExternal>()));

        var (Registrations, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Count(static item => item.Code == "agentkit.component-infrastructure.opaque-factory")
            .ShouldBe(1);
        Registrations.Length.ShouldBe(2);
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Materialize_WhenNonGenericDescriptorImplementationDoesNotImplementTheContract_ReportsInvalidRegistration()
    {
        var services = new ServiceCollection
        {
            new ServiceDescriptor(typeof(IExternal), typeof(UnrelatedToExternal), ServiceLifetime.Singleton),
        };
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.registration-invalid");
    }

    [Fact]
    public void Materialize_WhenOpenGenericImplementationIsRegisteredForANonGenericContract_ReportsInvalidGenericEvidence()
    {
        var services = new ServiceCollection
        {
            new ServiceDescriptor(typeof(INonGenericFromOpenGeneric), typeof(OpenGenericImplementingNonGeneric<>), ServiceLifetime.Singleton),
        };
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<INonGenericFromOpenGeneric>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.generic-closure-invalid");
    }

    [Fact]
    public void Materialize_WhenClosedGenericImplementationDoesNotImplementTheRequestedConstruction_ReportsInvalidGenericEvidence()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(typeof(IGenericExternal<>), typeof(ArrayWrappingExternal<>));
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IGenericExternal<string>>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.generic-closure-invalid");
    }

    [Fact]
    public void Materialize_WhenConstructorHasAnUnsupportedCollectionElementType_ReportsUnsupportedDependency()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IExternal, UnsupportedCollectionElementExternal>();
        var snapshot = Snapshot(services, RootRegistration(InfrastructureDependency<IExternal>()));

        var (_, Diagnostics, _) = ComponentInfrastructureGraphMaterializer.Materialize(snapshot);

        Diagnostics.Select(static item => item.Code)
            .ShouldContain("agentkit.component-infrastructure.dependency-unsupported");
    }

    private static ComponentRegistrationSnapshot Snapshot(
        IServiceCollection services,
        params ComponentRegistrationDescriptor[] registrations) => new(
            [.. services],
            [.. registrations]);

    private static ComponentRegistrationSnapshot SnapshotWithMaximum(
        IServiceCollection services,
        int maximumDerivedInfrastructureRegistrations,
        params ComponentRegistrationDescriptor[] registrations) => new(
            [.. services],
            [.. registrations],
            maximumDerivedInfrastructureRegistrations);

    private static ComponentRegistrationDescriptor RootRegistration(
        params ComponentDependencyDescriptor[] dependencies) => new(
            ComponentContractReference.Unkeyed<IInfrastructureRoot>(),
            typeof(InfrastructureRoot),
            ServiceLifetime.Singleton,
            [.. dependencies]);

    private static ComponentRegistrationDescriptor CollectionRootRegistration(
        params ComponentDependencyDescriptor[] dependencies) => new(
            ComponentContractReference.Unkeyed<ICollectionInfrastructureRoot>(),
            typeof(CollectionInfrastructureRoot),
            ServiceLifetime.Singleton,
            [.. dependencies]);

    private static ComponentRegistrationDescriptor Registration<TService, TImplementation>(
        ServiceLifetime lifetime)
        where TService : class
        where TImplementation : class, TService => new(
            ComponentContractReference.Unkeyed<TService>(),
            typeof(TImplementation),
            lifetime,
            []);

    private static ComponentDependencyDescriptor LoggerDependency() =>
        InfrastructureDependency<ILogger<LoggerCategory>>(ComponentDependencyCardinality.OptionalSingular);

    private static ComponentDependencyDescriptor InfrastructureDependency<TService>(
        ComponentDependencyCardinality cardinality = ComponentDependencyCardinality.RequiredSingular)
        where TService : class => new(
            ComponentContractReference.Unkeyed<TService>(),
            cardinality,
            factoryBoundary: null,
            ComponentDependencyValidationBoundary.MicrosoftDependencyInjectionInfrastructure);

    private interface IInfrastructureRoot;

    private sealed class InfrastructureRoot: IInfrastructureRoot;

    private interface ICollectionInfrastructureRoot;

    private sealed class CollectionInfrastructureRoot: ICollectionInfrastructureRoot;

    private sealed class LoggerCategory;

    private interface IExternal;

    private sealed class External: IExternal;

    private sealed class ExternalBackEdge: IExternal
    {
        public ExternalBackEdge(IInfrastructureRoot root)
        {
            ArgumentNullException.ThrowIfNull(root);
            Root = root;
        }

        public IInfrastructureRoot Root { get; }
    }

    private interface IGenericExternal<T>;

    private sealed class OpenExternal<T>: IGenericExternal<T>;

    private sealed class OtherOpenExternal<T>: IGenericExternal<T>;

    private sealed class ClosedStringExternal: IGenericExternal<string>;

    private sealed class OtherClosedStringExternal: IGenericExternal<string>;

    private sealed class StructOnlyExternal<T>: IGenericExternal<T>
        where T : struct;

    private interface INode<T>;

    private sealed class ExpandingNode<T>: INode<T>
    {
        public ExpandingNode(INode<List<T>> next)
        {
            ArgumentNullException.ThrowIfNull(next);
            Next = next;
        }

        public INode<List<T>> Next { get; }
    }

    private sealed class TerminalNestedStringListNode: INode<List<List<string>>>;

    private interface IFirstDependency;

    private sealed class FirstDependency: IFirstDependency;

    private interface ISecondDependency;

    private sealed class SecondDependency: ISecondDependency;

    private interface IOptionalDependency;

    private sealed class OptionalDependency: IOptionalDependency;

    private sealed class AmbiguousExternal: IExternal
    {
        public AmbiguousExternal(IFirstDependency first) => ArgumentNullException.ThrowIfNull(first);

        public AmbiguousExternal(ISecondDependency second) => ArgumentNullException.ThrowIfNull(second);
    }

    private sealed class UnresolvableExternal(IOptionalDependency dependency): IExternal
    {
        public IOptionalDependency Dependency { get; } = dependency;
    }

    private sealed class ServiceLocatorExternal(IServiceProvider provider): IExternal
    {
        public IServiceProvider Provider { get; } = provider;
    }

    private sealed class KeyedExternal(
        [FromKeyedServices("first")] IFirstDependency dependency): IExternal
    {
        public IFirstDependency Dependency { get; } = dependency;
    }

    private sealed class NullKeyFallbackExternal: IExternal
    {
        public NullKeyFallbackExternal()
        {
        }

        public NullKeyFallbackExternal(
            [FromKeyedServices(null)] IFirstDependency dependency) =>
            ArgumentNullException.ThrowIfNull(dependency);
    }

    private sealed class StringKeyedCollectionExternal: IExternal
    {
        public static int ActivationCount { get; private set; }

        public StringKeyedCollectionExternal() => ActivationCount++;

        public StringKeyedCollectionExternal(
            [FromKeyedServices("missing")] IEnumerable<IFirstDependency> dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependencies);
            ActivationCount++;
        }
    }

    private sealed class NullKeyedCollectionExternal: IExternal
    {
        public static int ActivationCount { get; private set; }

        public NullKeyedCollectionExternal() => ActivationCount++;

        public NullKeyedCollectionExternal(
            [FromKeyedServices(null)] IEnumerable<IFirstDependency> dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependencies);
            ActivationCount++;
        }
    }

    private interface IDeferredConsumer;

    private sealed class DeferredConsumer(Func<IExternal> factory): IDeferredConsumer
    {
        public Func<IExternal> Factory { get; } = factory;
    }

    private sealed class ThrowingKey
    {
        public int EqualsCalls { get; private set; }

        public override bool Equals(object? obj)
        {
            EqualsCalls++;
            throw new InvalidOperationException("Application key equality must not run during validation.");
        }

        public override int GetHashCode() => 0;
    }

    private sealed class DefaultAndCollectionExternal(
        IEnumerable<ISecondDependency> dependencies,
        IFirstDependency first,
        IOptionalDependency? optional = null): IExternal
    {
        public IEnumerable<ISecondDependency> Dependencies { get; } = dependencies;

        public IFirstDependency First { get; } = first;

        public IOptionalDependency? Optional { get; } = optional;
    }

    private sealed class PrimitiveDefaultsExternal(
        int capacity = 16,
        TestMode mode = TestMode.Default,
        string? name = null): IExternal
    {
        public int Capacity { get; } = capacity;

        public TestMode Mode { get; } = mode;

        public string? Name { get; } = name;
    }

    private sealed class OptionalReferenceExternal(IOptionalDependency? dependency = null): IExternal
    {
        public IOptionalDependency? Dependency { get; } = dependency;
    }

    private sealed class OptionalBackEdge(IInfrastructureRoot? root = null): IExternal
    {
        public IInfrastructureRoot? Root { get; } = root;
    }

    private sealed class UnrelatedToExternal;

    private interface INonGenericFromOpenGeneric;

    private sealed class OpenGenericImplementingNonGeneric<T>: INonGenericFromOpenGeneric;

    private sealed class ArrayWrappingExternal<T>: IGenericExternal<T[]>;

    private sealed class UnsupportedCollectionElementExternal(IEnumerable<int> values): IExternal
    {
        public IEnumerable<int> Values { get; } = values;
    }

    private enum TestMode
    {
        Default = 0,
    }
}
