// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;

/// <summary>Verifies that selecting a compaction profile forces a published profile and a registered compactor at composition.</summary>
public sealed class CompactionCompositionValidatorTests
{
    private static readonly CompactionProfileKey _profile = CompactionPolicyFixtures.ProfileKey;
    private static readonly ComponentKey<ICompactor> _compactor = CompactionPolicyFixtures.CompactorKey;

    [Fact]
    public void Validate_WhenNoDefinitionSelectsAProfile_ReportsNothingAndNeverResolvesTheCatalog()
    {
        var provider = new ThrowingCatalogProvider();

        var diagnostics = Validate([CompositionTestData.Definition()], provider, Registrations(compactor: false));

        diagnostics.ShouldBeEmpty();
        provider.Resolutions.ShouldBe(0);
    }

    [Fact]
    public void Validate_WhenTheProfileAndCompactorAreRegistered_ReportsNothing() =>
        Validate(Definitions(), Provider(CompactionPolicyFixtures.Publication()), Registrations(compactor: true)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenNoCatalogIsRegistered_ReportsTheProfileMissing() =>
        Validate(Definitions(), Provider(catalog: null), Registrations(compactor: true))
            .Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.definition.compaction.missing"]);

    [Fact]
    public void Validate_WhenTheCatalogDoesNotPublishTheProfile_ReportsTheProfileMissing() =>
        Validate(
                Definitions(),
                Provider(CompactionPolicyFixtures.Publication(profileKey: new CompactionProfileKey("other"))),
                Registrations(compactor: true))
            .Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.definition.compaction.missing"]);

    [Fact]
    public void Validate_WhenTheProfileNamesAnUnregisteredCompactor_ReportsTheCompactorMissing()
    {
        var diagnostics = Validate(Definitions(), Provider(CompactionPolicyFixtures.Publication()), Registrations(compactor: false));

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe("agentkit.definition.compaction.compactor-missing");
        diagnostic.SafeMessage.ShouldContain(_compactor.Value);
    }

    [Fact]
    public void Validate_WhenOnlyAnUnkeyedCompactorIsRegistered_StillRequiresTheProfilesExactKey()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ICompactor>(static _ => throw new InvalidOperationException("never resolved"));

        var diagnostics = Validate(Definitions(), Provider(CompactionPolicyFixtures.Publication()), ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.definition.compaction.compactor-missing"]);
    }

    [Fact]
    public void Validate_WhenTheProfileIsDisabled_DoesNotRequireACompactor() =>
        Validate(Definitions(), Provider(CompactionPolicyFixtures.Publication(enabled: false)), Registrations(compactor: false)).ShouldBeEmpty();

    [Fact]
    public void Validate_WhenTwoDefinitionsShareAMissingCompactor_ReportsItOnce()
    {
        var first = CompositionTestData.Definition(new AgentId(Guid.Parse("c0000000-0000-0000-0000-000000000001"))) with { OptionalCapabilities = Selection() };
        var second = CompositionTestData.Definition(new AgentId(Guid.Parse("c0000000-0000-0000-0000-000000000002"))) with { OptionalCapabilities = Selection() };

        var diagnostics = Validate([first, second], Provider(CompactionPolicyFixtures.Publication()), Registrations(compactor: false));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.definition.compaction.compactor-missing"]);
    }

    [Fact]
    public void Validate_WhenTheCatalogCannotBeBuilt_ReportsItsMessageAsProfileInvalid()
    {
        var diagnostics = Validate(Definitions(), new ThrowingCatalogProvider(), Registrations(compactor: true));

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe("agentkit.definition.compaction.profile-invalid");
        diagnostic.SafeMessage.ShouldContain("catalog failure");
    }

    [Fact]
    public void Validate_WhenAProfileIsSelected_NeverActivatesTheCompactor()
    {
        var activations = 0;
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<ICompactor>(_compactor.Value, (_, _) =>
        {
            activations++;
            throw new InvalidOperationException("Composition validation must not activate the compactor.");
        });

        var diagnostics = Validate(Definitions(), Provider(CompactionPolicyFixtures.Publication()), ComponentRegistrationSnapshot.Capture(services));

        diagnostics.ShouldBeEmpty();
        activations.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenACompactionProfileIsSelectedButNotPublished_RejectsWithCompactionMissing()
    {
        var builder = CompositionTestData.RunnableBuilder(definition: CompositionTestData.Definition() with { OptionalCapabilities = Selection() });

        Codes(builder).ShouldContain("agentkit.definition.compaction.missing");
    }

    [Fact]
    public void Build_WhenAPublishedProfileNamesAnUnregisteredCompactor_RejectsWithCompactorMissing()
    {
        var builder = CompositionTestData.RunnableBuilder(definition: CompositionTestData.Definition() with { OptionalCapabilities = Selection() });
        _ = builder.Services.AddSingleton<ICompactionProfileCatalog>(new StaticCompactionProfileCatalog(CompactionPolicyFixtures.Publication()));

        Codes(builder).ShouldContain("agentkit.definition.compaction.compactor-missing");
    }

    [Fact]
    public async Task Build_WhenTheProfileIsPublishedAndItsCompactorIsRegistered_Accepts()
    {
        var builder = CompositionTestData.RunnableBuilder(definition: CompositionTestData.Definition() with { OptionalCapabilities = Selection() });
        _ = builder.Services.AddSingleton<ICompactionProfileCatalog>(new StaticCompactionProfileCatalog(CompactionPolicyFixtures.Publication()));
        _ = builder.Services.AddKeyedSingleton<ICompactor>(_compactor.Value, static (_, _) => throw new InvalidOperationException("never resolved"));

        await using var engine = builder.Build();

        _ = engine;
    }

    private static string[] Codes(AgentEngineBuilder builder) =>
        [.. Should.Throw<AgentCompositionException>(builder.Build).Diagnostics.Select(static diagnostic => diagnostic.Code)];

    private static ImmutableArray<CompositionDiagnostic> Validate(
        ImmutableArray<AgentDefinition> definitions, IServiceProvider provider, ComponentRegistrationSnapshot registrations)
    {
        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        CompactionCompositionValidator.Validate(definitions, provider, registrations, diagnostics);
        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<AgentDefinition> Definitions() =>
        [CompositionTestData.Definition() with { OptionalCapabilities = Selection() }];

    private static AgentOptionalCapabilitySelection Selection() =>
        AgentOptionalCapabilitySelection.None with { CompactionProfile = _profile };

    private static ComponentRegistrationSnapshot Registrations(bool compactor)
    {
        var services = new ServiceCollection();
        if (compactor)
        {
            _ = services.AddKeyedSingleton<ICompactor>(_compactor.Value, static (_, _) => throw new InvalidOperationException("never resolved"));
        }

        return ComponentRegistrationSnapshot.Capture(services);
    }

    private static ServiceProvider Provider(CompactionProfilePublication? publication) =>
        Provider(publication is null ? null : new StaticCompactionProfileCatalog(publication));

    private static ServiceProvider Provider(ICompactionProfileCatalog? catalog)
    {
        var services = new ServiceCollection();
        if (catalog is not null)
        {
            _ = services.AddSingleton(catalog);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>A provider whose catalog resolution fails the way a profile that cannot be compiled does.</summary>
    private sealed class ThrowingCatalogProvider: IServiceProvider
    {
        public int Resolutions { get; private set; }

        public object? GetService(Type serviceType)
        {
            if (serviceType != typeof(ICompactionProfileCatalog))
            {
                return null;
            }

            Resolutions++;
            throw new InvalidOperationException("catalog failure");
        }
    }
}
