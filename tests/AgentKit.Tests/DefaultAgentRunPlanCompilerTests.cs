// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Internal;

/// <summary>Verifies how DefaultAgentRunPlanCompiler resolves a definition's compaction selection.</summary>
public sealed class DefaultAgentRunPlanCompilerTests
{
    private static readonly CompactionProfileKey _profile = CompactionPolicyFixtures.ProfileKey;
    private static readonly ComponentKey<ICompactor> _compactor = CompactionPolicyFixtures.CompactorKey;

    [Fact]
    public void ResolveCompaction_WhenProviderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DefaultAgentRunPlanCompiler.ResolveCompaction(null!, Definition(selectProfile: false)))
            .ParamName.ShouldBe("provider");

    [Fact]
    public void ResolveCompaction_WhenDefinitionIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DefaultAgentRunPlanCompiler.ResolveCompaction(new ServiceCollection().BuildServiceProvider(), null!))
            .ParamName.ShouldBe("definition");

    [Fact]
    public void ResolveCompaction_WhenNoProfileIsSelectedAndAnUnkeyedCompactorExists_ReturnsItWithoutAPolicy()
    {
        var unkeyed = new StubCompactor();
        using var provider = Provider(services => services.AddSingleton<ICompactor>(unkeyed));

        var selection = DefaultAgentRunPlanCompiler.ResolveCompaction(provider, Definition(selectProfile: false));

        selection.Compactor.ShouldBeSameAs(unkeyed);
        selection.Policy.ShouldBeNull();
        selection.Disabled.ShouldBeFalse();
    }

    [Fact]
    public void ResolveCompaction_WhenNoProfileIsSelectedAndNoCompactorExists_ReturnsNothing()
    {
        using var provider = Provider(static _ => { });

        var selection = DefaultAgentRunPlanCompiler.ResolveCompaction(provider, Definition(selectProfile: false));

        selection.Compactor.ShouldBeNull();
        selection.Policy.ShouldBeNull();
        selection.Disabled.ShouldBeFalse();
    }

    [Fact]
    public void ResolveCompaction_WhenAnEnabledProfileIsSelected_ReturnsTheKeyedCompactorAndThePublishedPolicy()
    {
        var publication = CompactionPolicyFixtures.Publication();
        var keyed = new StubCompactor();
        using var provider = Provider(services =>
        {
            _ = services.AddSingleton<ICompactionProfileCatalog>(new StaticCompactionProfileCatalog(publication));
            _ = services.AddKeyedSingleton<ICompactor>(_compactor.Value, keyed);
            _ = services.AddSingleton<ICompactor>(new StubCompactor());
        });

        var selection = DefaultAgentRunPlanCompiler.ResolveCompaction(provider, Definition(selectProfile: true));

        selection.Compactor.ShouldBeSameAs(keyed);
        selection.Policy.ShouldBeSameAs(publication.Policy);
        selection.Disabled.ShouldBeFalse();
    }

    [Fact]
    public void ResolveCompaction_WhenTheSelectedProfileIsDisabled_ReturnsDisabledWithoutACompactor()
    {
        using var provider = Provider(services =>
        {
            _ = services.AddSingleton<ICompactionProfileCatalog>(new StaticCompactionProfileCatalog(CompactionPolicyFixtures.Publication(enabled: false)));
            _ = services.AddSingleton<ICompactor>(new StubCompactor());
        });

        var selection = DefaultAgentRunPlanCompiler.ResolveCompaction(provider, Definition(selectProfile: true));

        selection.Compactor.ShouldBeNull();
        selection.Policy.ShouldBeNull();
        selection.Disabled.ShouldBeTrue();
    }

    [Fact]
    public void ResolveCompaction_WhenTheSelectedProfileIsNotPublished_ThrowsInvalidOperationException()
    {
        using var provider = Provider(services =>
            services.AddSingleton<ICompactionProfileCatalog>(new StaticCompactionProfileCatalog()));

        var failure = Should.Throw<InvalidOperationException>(
            () => DefaultAgentRunPlanCompiler.ResolveCompaction(provider, Definition(selectProfile: true)));

        failure.Message.ShouldContain(_profile.Value);
    }

    [Fact]
    public void ResolveCompaction_WhenTheProfilesCompactorHasNoKeyedRegistration_ThrowsInsteadOfFallingBackToTheUnkeyedCompactor()
    {
        using var provider = Provider(services =>
        {
            _ = services.AddSingleton<ICompactionProfileCatalog>(new StaticCompactionProfileCatalog(CompactionPolicyFixtures.Publication()));
            _ = services.AddSingleton<ICompactor>(new StubCompactor());
        });

        _ = Should.Throw<InvalidOperationException>(
            () => DefaultAgentRunPlanCompiler.ResolveCompaction(provider, Definition(selectProfile: true)));
    }

    private static ServiceProvider Provider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return services.BuildServiceProvider();
    }

    private static AgentDefinition Definition(bool selectProfile) =>
        selectProfile
            ? CompositionTestData.Definition() with { OptionalCapabilities = AgentOptionalCapabilitySelection.None with { CompactionProfile = _profile } }
            : CompositionTestData.Definition();

    /// <summary>A compactor that is only ever compared by identity.</summary>
    private sealed class StubCompactor: ICompactor
    {
        public Task<CompactionResult> CompactAsync(CompactionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CompactionResult> CompactAsync(
            CompactionRequest request,
            SessionExecutionCapability session,
            BudgetExecutionCapability budget,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
