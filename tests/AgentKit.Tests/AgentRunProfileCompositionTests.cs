// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class AgentRunProfileCompositionTests
{
    [Fact]
    public void Build_WhenDefinitionUsesLegacyUnconfiguredShape_RejectsAsUnrunnable()
    {
        var configured = CompositionTestData.Definition();
        var legacy = new AgentDefinition(
            configured.Id,
            configured.Revision,
            configured.DisplayName,
            configured.Models,
            configured.ModelRequirements,
            configured.Instructions,
            configured.Tools,
            configured.ToolChoice,
            configured.Settings,
            configured.RunDefaults,
            configured.Extensions);
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddAgent(legacy);
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(
            diagnostic => diagnostic.Code == "agentkit.definition.profiles.missing");
    }

    [Fact]
    public void Build_WhenRunProfileReaderIsNotReady_RejectsComposition()
    {
        var definition = CompositionTestData.Definition();
        var reader = new MutableRunProfilePublicationReader(
            currentSnapshot: null,
            new AgentRunProfilePublicationUnavailable("Not ready."));
        var builder = Builder(definition, reader);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.run-profile.not-ready");
        reader.Reads.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenExactRunProfileIsMissing_RejectsComposition()
    {
        var definition = CompositionTestData.Definition();
        var reader = new MutableRunProfilePublicationReader(
            new AgentRunProfilePublicationSnapshot([]),
            new AgentRunProfilePublicationUnavailable("Missing."));
        var builder = Builder(definition, reader);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.run-profile.missing");
        reader.Reads.ShouldBe(0);
    }

    [Fact]
    public void Build_WhenDefinitionProfileKeysDifferFromPublication_RejectsComposition()
    {
        var definition = CompositionTestData.Definition();
        var publication = CompositionTestData.RunProfile(definition);
        var mismatched = new AgentRunProfilePublication(
            new SecurityProfilePublication(
                definition.Id,
                definition.Revision,
                publication.SecurityProfile.ConfigurationVersion,
                new SecurityProfileKey("different"),
                publication.SecurityProfile.ProfileVersion,
                publication.SecurityProfile.PolicySnapshot,
                publication.SecurityProfile.AuthorityKey),
            publication.SessionProfile);
        var reader = new MutableRunProfilePublicationReader(
            new AgentRunProfilePublicationSnapshot([mismatched]),
            new AgentRunProfilePublicationFound(mismatched));
        var builder = Builder(definition, reader);

        var exception = Should.Throw<AgentCompositionException>(builder.Build);

        exception.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "agentkit.run-profile.key-mismatch");
    }

    [Fact]
    public void Build_WhenExactRunProfileCoordinatesAreDuplicated_RejectsActivation()
    {
        var definition = CompositionTestData.Definition();
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddAgentRunProfilePublication(CompositionTestData.RunProfile(definition));

        var exception = Should.Throw<ArgumentException>(builder.Build);

        exception.ParamName.ShouldBe("publications");
    }

    [Fact]
    public async Task RunAsync_WhenRuntimePublicationDiffersFromPinnedSnapshot_RejectsBeforeIdentifiersOrLoop()
    {
        var definition = CompositionTestData.Definition();
        var pinned = CompositionTestData.RunProfile(definition);
        var changed = new AgentRunProfilePublication(
            new SecurityProfilePublication(
                definition.Id,
                definition.Revision,
                new ConfigurationVersion(2),
                pinned.SecurityProfile.ProfileKey,
                pinned.SecurityProfile.ProfileVersion,
                pinned.SecurityProfile.PolicySnapshot,
                pinned.SecurityProfile.AuthorityKey),
            pinned.SessionProfile);
        var reader = new MutableRunProfilePublicationReader(
            new AgentRunProfilePublicationSnapshot([pinned]),
            new AgentRunProfilePublicationFound(pinned));
        var selector = new TestSecurityProfileSelector();
        var runIds = new CountingRunIdGenerator();
        var loop = new RecordingAgentLoop();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddSingleton<IAgentLoop>(loop);
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(selector);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentRunProfilePublicationReader>(reader));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        reader.Result = new AgentRunProfilePublicationFound(changed);

        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(
                CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        reader.Reads.ShouldBe(1);
        runIds.Created.ShouldBe(0);
        selector.Requests.ShouldBeEmpty();
        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_WhenReaderAlternatesReadiness_PinsOnlyTheSnapshotActuallyValidated()
    {
        var definition = CompositionTestData.Definition();
        var validated = CompositionTestData.RunProfile(definition);
        var replacement = new AgentRunProfilePublication(
            new SecurityProfilePublication(
                definition.Id,
                definition.Revision,
                new ConfigurationVersion(2),
                validated.SecurityProfile.ProfileKey,
                validated.SecurityProfile.ProfileVersion,
                validated.SecurityProfile.PolicySnapshot,
                validated.SecurityProfile.AuthorityKey),
            validated.SessionProfile);
        var reader = new AlternatingRunProfilePublicationReader(
            new AgentRunProfilePublicationSnapshot([validated]),
            new AgentRunProfilePublicationSnapshot([replacement]),
            new AgentRunProfilePublicationFound(replacement));
        var runIds = new CountingRunIdGenerator();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentRunProfilePublicationReader>(reader));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(
                CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        reader.SnapshotReads.ShouldBe(1);
        runIds.Created.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WhenPublicationAndCaptureMatch_PassesExactEvidenceToLoop()
    {
        var definition = CompositionTestData.Definition();
        var selector = new TestSecurityProfileSelector();
        var loop = new RecordingAgentLoop();
        var builder = CompositionTestData.RunnableBuilder(loop, definition);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<ISecurityProfileSelector>(selector));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        _ = await agent.RunAsync(
            CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);

        var capture = selector.Requests.ShouldHaveSingleItem();
        var request = loop.ReceivedRequests.ShouldHaveSingleItem();
        request.Authorization.Scope.ShouldBe(capture.Scope);
        request.Authorization.Identity.ShouldBe(capture.Identity);
        request.SessionProfile.ShouldBe(CompositionTestData.RunProfile(definition).SessionProfile);
    }

    [Fact]
    public async Task RunAsync_WhenFreshCaptureDiffersFromPinnedEvidence_RejectsBeforeLoopScope()
    {
        var definition = CompositionTestData.Definition();
        var selector = new TestSecurityProfileSelector
        {
            ProfileVersion = new SecurityProfileVersion(2),
        };
        var effects = new AdmissionRunEffects();
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<ISecurityProfileSelector>(selector));
        _ = builder.Services.Replace(
            ServiceDescriptor.Scoped<IAgentLoop>(_ => new ScopedRecordingAgentLoop(effects)));
        await using var engine = builder.Build();
        var baselineScopes = effects.Scopes;
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(
                CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        _ = selector.Requests.ShouldHaveSingleItem();
        effects.Scopes.ShouldBe(baselineScopes);
        effects.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_WhenRunProfileReadCancelsBeforeReturning_PropagatesBeforeDependentWork(
        bool returnsFound)
    {
        var definition = CompositionTestData.Definition();
        var publication = CompositionTestData.RunProfile(definition);
        using var cancellation = new CancellationTokenSource();
        var reader = new CancellingRunProfilePublicationReader(
            new AgentRunProfilePublicationSnapshot([publication]),
            returnsFound
                ? new AgentRunProfilePublicationFound(publication)
                : new AgentRunProfilePublicationUnavailable("Cancelled read returned unavailable."),
            cancellation);
        var selector = new TestSecurityProfileSelector();
        var runIds = new CountingRunIdGenerator();
        var loop = new RecordingAgentLoop();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddSingleton<IAgentLoop>(loop);
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(selector);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentRunProfilePublicationReader>(reader));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        var exception = await Should.ThrowAsync<OperationCanceledException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        runIds.Created.ShouldBe(0);
        selector.Requests.ShouldBeEmpty();
        loop.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenProfileCaptureCancelsBeforeReturning_PropagatesBeforeRunScope()
    {
        var definition = CompositionTestData.Definition();
        using var cancellation = new CancellationTokenSource();
        var selector = new TestSecurityProfileSelector { CancellationSource = cancellation };
        var effects = new AdmissionRunEffects();
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<ISecurityProfileSelector>(selector));
        _ = builder.Services.Replace(
            ServiceDescriptor.Scoped<IAgentLoop>(_ => new ScopedRecordingAgentLoop(effects)));
        await using var engine = builder.Build();
        var baselineScopes = effects.Scopes;
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        var exception = await Should.ThrowAsync<OperationCanceledException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        _ = selector.Requests.ShouldHaveSingleItem();
        effects.Scopes.ShouldBe(baselineScopes);
        effects.Requests.ShouldBeEmpty();
    }

    private static AgentEngineBuilder Builder(
        AgentDefinition definition,
        IAgentRunProfilePublicationReader reader)
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddSingleton<IAgentLoop>(new RecordingAgentLoop());
        _ = builder.Services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        _ = builder.Services.Replace(
            ServiceDescriptor.Singleton(reader));
        return builder;
    }
}
