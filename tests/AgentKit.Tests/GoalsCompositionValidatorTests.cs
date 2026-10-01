// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies that selecting a goal profile forces its whole runtime path to exist at composition.</summary>
public sealed class GoalsCompositionValidatorTests
{
    private static readonly GoalProfileKey _profile = new("goals");
    private static readonly GoalStoreKey _store = new("store");
    private static readonly DelegationDispatcherKey _dispatcher = new("dispatcher");

    private static readonly (Type Service, string Code)[] _singular =
    [
        (typeof(IGoalCoordinator), "agentkit.goals.coordinator.missing"),
        (typeof(IDelegationCoordinator), "agentkit.goals.delegation-coordinator.missing"),
        (typeof(IGoalStoreSelector), "agentkit.goals.store-selector.missing"),
        (typeof(IDelegationDispatcherSelector), "agentkit.goals.dispatcher-selector.missing"),
        (typeof(IGoalJoinStrategySelector), "agentkit.goals.join-selector.missing"),
        (typeof(IDelegationTargetCatalog), "agentkit.goals.target-catalog.missing"),
        (typeof(IDelegationTargetSelector), "agentkit.goals.target-selector.missing"),
        (typeof(IDelegationPolicyPipeline), "agentkit.goals.policy-pipeline.missing"),
        (typeof(IGoalBudgetManager), "agentkit.goals.budget-manager.missing"),
        (typeof(IGoalEventDispatcher), "agentkit.goals.event-dispatcher.missing"),
        (typeof(IGoalProfileCatalog), "agentkit.goals.profile-catalog.missing"),
    ];

    public static TheoryData<Type, string> SingularServices
    {
        get
        {
            var data = new TheoryData<Type, string>();
            foreach (var (service, code) in _singular)
            {
                data.Add(service, code);
            }

            return data;
        }
    }

    [Fact]
    public void Validate_WhenNoDefinitionSelectsAGoalProfile_ReportsNothing() =>
        Validate(Definitions(goals: false), catalog: null, Registrations(complete: false)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenTheCompositionIsComplete_ReportsNothing() =>
        Validate(Definitions(goals: true), new StubCatalog(_profile), Registrations(complete: true)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenNoProfileCatalogIsRegistered_ReportsOnlyTheMissingProfile()
    {
        var diagnostics = Validate(Definitions(goals: true), catalog: null, Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.goals.profile.missing"]);
    }

    [Fact]
    public void Validate_WhenTheSelectedProfileIsNotPublished_ReportsTheMissingProfile()
    {
        var diagnostics = Validate(Definitions(goals: true), new StubCatalog(new GoalProfileKey("other")), Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.goals.profile.missing"]);
    }

    [Theory]
    [MemberData(nameof(SingularServices))]
    public void Validate_WhenASingularServiceIsMissing_ReportsThatExactDiagnostic(Type omitted, string code) =>
        Validate(Definitions(goals: true), new StubCatalog(_profile), Registrations(complete: true, omitUnkeyed: omitted))
            .Select(static diagnostic => diagnostic.Code).ShouldBe([code]);

    [Theory]
    [InlineData("store", "agentkit.goals.store.missing")]
    [InlineData("dispatcher", "agentkit.goals.dispatcher.missing")]
    [InlineData("join", "agentkit.goals.join-strategy.missing")]
    public void Validate_WhenAKeyedComponentIsMissing_ReportsThatExactDiagnostic(string omitted, string code) =>
        Validate(Definitions(goals: true), new StubCatalog(_profile), Registrations(complete: true, omitKeyed: omitted))
            .Select(static diagnostic => diagnostic.Code).ShouldBe([code]);

    [Fact]
    public void Validate_WhenAComponentIsRegisteredUnkeyed_StillRequiresTheProfilesExactKey()
    {
        var services = Services(complete: true, omitKeyed: "store");
        _ = services.AddSingleton<IGoalStore>(static _ => throw new InvalidOperationException("never resolved"));

        var diagnostics = Validate(Definitions(goals: true), new StubCatalog(_profile), ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.goals.store.missing"]);
    }

    [Fact]
    public void Validate_WhenNothingIsRegistered_ReportsEveryGapExactlyOnce()
    {
        var diagnostics = Validate(Definitions(goals: true), new StubCatalog(_profile), Registrations(complete: false));

        diagnostics.Count(static diagnostic => diagnostic.Code == "agentkit.goals.coordinator.missing").ShouldBe(1);
        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.goals.store.missing");
        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.goals.dispatcher.missing");
        diagnostics.Length.ShouldBe(_singular.Length + 3);
    }

    [Fact]
    public void Validate_WhenTwoDefinitionsShareAProfile_ReportsEachGapOnlyOnce()
    {
        var first = CompositionTestData.Definition(new AgentId(Guid.Parse("c0000000-0000-0000-0000-000000000001"))) with { OptionalCapabilities = Selection() };
        var second = CompositionTestData.Definition(new AgentId(Guid.Parse("c0000000-0000-0000-0000-000000000002"))) with { OptionalCapabilities = Selection() };

        var diagnostics = Validate([first, second], new StubCatalog(_profile), Registrations(complete: true, omitKeyed: "store", omitUnkeyed: typeof(IGoalCoordinator)));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.goals.coordinator.missing", "agentkit.goals.store.missing"]);
    }

    [Fact]
    public void Validate_WhenAProfileIsSelected_NeverActivatesAStoreOrDispatcher()
    {
        var activations = 0;
        var services = Services(complete: true, omitKeyed: "store");
        _ = services.AddKeyedSingleton<IGoalStore>("store", (_, _) =>
        {
            activations++;
            throw new InvalidOperationException("Composition validation must not activate the store.");
        });

        var diagnostics = Validate(Definitions(goals: true), new StubCatalog(_profile), ComponentRegistrationSnapshot.Capture(services));

        diagnostics.ShouldBeEmpty();
        activations.ShouldBe(0);
    }

    private static ImmutableArray<CompositionDiagnostic> Validate(ImmutableArray<AgentDefinition> definitions, IGoalProfileCatalog? catalog, ComponentRegistrationSnapshot registrations)
    {
        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        GoalsCompositionValidator.Validate(definitions, catalog, registrations, diagnostics);
        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<AgentDefinition> Definitions(bool goals) =>
        [goals ? CompositionTestData.Definition() with { OptionalCapabilities = Selection() } : CompositionTestData.Definition()];

    private static AgentOptionalCapabilitySelection Selection() => new(null, null, null, null, _profile, []);

    private static ComponentRegistrationSnapshot Registrations(bool complete, Type? omitUnkeyed = null, string? omitKeyed = null) =>
        ComponentRegistrationSnapshot.Capture(Services(complete, omitUnkeyed, omitKeyed));

    private static ServiceCollection Services(bool complete, Type? omitUnkeyed = null, string? omitKeyed = null)
    {
        var services = new ServiceCollection();
        if (!complete)
        {
            return services;
        }

        foreach (var (service, _) in _singular)
        {
            if (service != omitUnkeyed)
            {
                _ = services.Add(ServiceDescriptor.Singleton(service, static _ => throw new InvalidOperationException("never resolved")));
            }
        }

        if (omitKeyed != "store")
        {
            _ = services.AddKeyedSingleton<IGoalStore>(_store.Value, static (_, _) => throw new InvalidOperationException("never resolved"));
        }

        if (omitKeyed != "dispatcher")
        {
            _ = services.AddKeyedSingleton<IDelegationDispatcher>(_dispatcher.Value, static (_, _) => throw new InvalidOperationException("never resolved"));
        }

        if (omitKeyed != "join")
        {
            _ = services.AddKeyedSingleton<IGoalJoinStrategy>(GoalJoinStrategyKeys.All.Value, static (_, _) => throw new InvalidOperationException("never resolved"));
        }

        return services;
    }

    /// <summary>Publishes exactly one profile under the key the test registered.</summary>
    private sealed class StubCatalog(GoalProfileKey registered): IGoalProfileCatalog
    {
        public bool TryGet(GoalProfileKey key, [NotNullWhen(true)] out GoalProfileSnapshot? profile)
        {
            profile = key == registered
                ? new GoalProfileSnapshot(
                    registered, new GoalProfileVersion(1), _store, _dispatcher, [], [GoalJoinStrategyKeys.All], GoalJoinStrategyKeys.All,
                    4, 8, 4, DelegationFailureMode.SettleAllChildren, new GoalBudget(10, 10, 4), new ContentHash("sha256:test"))
                : null;
            return profile is not null;
        }

        public bool TryGet(GoalProfileReference reference, [NotNullWhen(true)] out GoalProfileSnapshot? profile) => TryGet(reference.Key, out profile);
    }
}
