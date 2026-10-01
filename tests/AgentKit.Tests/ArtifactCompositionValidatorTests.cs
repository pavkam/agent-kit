// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies that selecting an artifact coordinator forces its routed stores and engine services to exist at composition.</summary>
public sealed class ArtifactCompositionValidatorTests
{
    private static readonly ComponentKey<IArtifactCoordinator> _key = new("artifacts");

    [Fact]
    public void Validate_WhenNoDefinitionSelectsACoordinator_ReportsNothing() =>
        Validate(Definitions(selected: false), catalog: null, failure: null, Registrations(complete: false)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenTheCompositionIsComplete_ReportsNothing() =>
        Validate(Definitions(selected: true), new StubCatalog(_key, "a", "b"), failure: null, Registrations(complete: true)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenTheCoordinatorIsNotRegisteredUnderTheKey_ReportsItOnce() =>
        Validate(Definitions(selected: true, count: 2), new StubCatalog(_key, "a", "b"), failure: null, Registrations(complete: true, omitCoordinator: true))
            .Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.artifact.coordinator.missing"]);

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    public void Validate_WhenARoutedBackendHasNoKeyedStore_ReportsThatBackend(string omitted)
    {
        var diagnostics = Validate(Definitions(selected: true), new StubCatalog(_key, "a", "b"), failure: null, Registrations(complete: true, omitStore: omitted));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.artifact.store.missing"]);
        diagnostics[0].SafeMessage.ShouldContain($"'{omitted}'");
    }

    [Fact]
    public void Validate_WhenAStoreIsRegisteredUnkeyed_StillRequiresTheBackendsExactKey()
    {
        var services = Services(complete: true, omitCoordinator: false, omitStore: "a");
        _ = services.AddSingleton<IArtifactStore>(static _ => throw new InvalidOperationException("never resolved"));

        Validate(Definitions(selected: true), new StubCatalog(_key, "a", "b"), failure: null, ComponentRegistrationSnapshot.Capture(services))
            .Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.artifact.store.missing"]);
    }

    [Fact]
    public void Validate_WhenACoordinatorIsReplacedByAnApplicationType_OnlyRequiresItsKeyedRegistration() =>
        Validate(Definitions(selected: true), catalog: null, failure: null, Registrations(complete: true)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenTheCatalogFailedToBuild_ReportsTheFailureOnce()
    {
        var diagnostics = Validate(Definitions(selected: true, count: 2), catalog: null, failure: "Artifact profile 'x' is not registered.", Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.artifact.profile.invalid"]);
    }

    [Theory]
    [InlineData(typeof(ISecurityAuthoritySelector), "agentkit.artifact.security-authority-selector.missing")]
    [InlineData(typeof(TimeProvider), "agentkit.artifact.time-provider.missing")]
    public void Validate_WhenAnEngineServiceIsMissing_ReportsThatExactDiagnostic(Type omitted, string code) =>
        Validate(Definitions(selected: true, count: 2), new StubCatalog(_key, "a", "b"), failure: null, Registrations(complete: true, omitUnkeyed: omitted))
            .Select(static diagnostic => diagnostic.Code).ShouldBe([code]);

    [Fact]
    public void Validate_WhenACoordinatorIsSelected_NeverActivatesAnyComponent()
    {
        var activations = 0;
        var services = Services(complete: true, omitCoordinator: false, omitStore: "a");
        _ = services.AddKeyedSingleton<IArtifactStore>("a", (_, _) =>
        {
            activations++;
            throw new InvalidOperationException("Composition validation must not activate a store.");
        });

        Validate(Definitions(selected: true), new StubCatalog(_key, "a", "b"), failure: null, ComponentRegistrationSnapshot.Capture(services)).ShouldBeEmpty();

        activations.ShouldBe(0);
    }

    private static ImmutableArray<CompositionDiagnostic> Validate(
        ImmutableArray<AgentDefinition> definitions, IArtifactCoordinatorCatalog? catalog, string? failure, ComponentRegistrationSnapshot registrations)
    {
        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        ArtifactCompositionValidator.Validate(definitions, catalog, failure, registrations, diagnostics);
        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<AgentDefinition> Definitions(bool selected, int count = 1) =>
        [.. Enumerable.Range(1, count).Select(index =>
        {
            var definition = CompositionTestData.Definition(new AgentId(Guid.Parse($"c2000000-0000-0000-0000-{index:D12}")));
            return selected ? definition with { OptionalCapabilities = new AgentOptionalCapabilitySelection(null, _key, null, null, null, []) } : definition;
        })];

    private static ComponentRegistrationSnapshot Registrations(bool complete, Type? omitUnkeyed = null, bool omitCoordinator = false, string? omitStore = null) =>
        ComponentRegistrationSnapshot.Capture(Services(complete, omitUnkeyed, omitCoordinator, omitStore));

    private static ServiceCollection Services(bool complete, Type? omitUnkeyed = null, bool omitCoordinator = false, string? omitStore = null)
    {
        var services = new ServiceCollection();
        if (!complete)
        {
            return services;
        }

        foreach (var service in new[] { typeof(ISecurityAuthoritySelector), typeof(TimeProvider) })
        {
            if (service != omitUnkeyed)
            {
                _ = services.Add(ServiceDescriptor.Singleton(service, static _ => throw new InvalidOperationException("never resolved")));
            }
        }

        if (!omitCoordinator)
        {
            _ = services.AddKeyedSingleton<IArtifactCoordinator>(_key.Value, static (_, _) => throw new InvalidOperationException("never resolved"));
        }

        foreach (var name in new[] { "a", "b" })
        {
            if (name != omitStore)
            {
                _ = services.AddKeyedSingleton<IArtifactStore>(name, static (_, _) => throw new InvalidOperationException("never resolved"));
            }
        }

        return services;
    }

    /// <summary>Publishes exactly one coordinator routed to the named backends.</summary>
    private sealed class StubCatalog(ComponentKey<IArtifactCoordinator> registered, params string[] backends): IArtifactCoordinatorCatalog
    {
        public bool TryGet(ComponentKey<IArtifactCoordinator> key, [NotNullWhen(true)] out ArtifactCoordinatorSnapshot? snapshot)
        {
            snapshot = key == registered
                ? new ArtifactCoordinatorSnapshot(key, new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), [.. backends.Select(static backend => new ArtifactBackendKey(backend))])
                : null;
            return snapshot is not null;
        }
    }
}
