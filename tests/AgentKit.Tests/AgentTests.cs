// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

using AgentKit.IO;
using AgentKit.Observability;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

/// <summary>Verifies Agent behavior and contracts.</summary>
[Collection(AdmissionObservabilityGroup.Name)]
public sealed class AgentTests
{
    [Fact]
    public async Task RunAsync_WhenPinnedDefinitionIsRemoved_RejectsBeforeAllocatingRunOrScope()
    {
        var definition = DefinitionWithInstruction();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        var runIds = new CountingRunIdGenerator();
        await using var engine = Build(catalog, effects, runIds);
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        var baselineScopes = effects.Scopes;
        catalog.Publish(2);
        var rejected = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        rejected.AgentId.ShouldBe(definition.Id);
        rejected.Failure.SafeMessage.ShouldContain("no longer enabled");
        effects.Requests.ShouldBeEmpty();
        effects.Scopes.ShouldBe(baselineScopes);
        runIds.Created.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WhenPinnedDefinitionIsRemoved_EmitsCorrelatedRejectedAdmissionActivity()
    {
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var catalog = new MutableAgentDefinitionCatalog(definition);
        await using var engine = Build(catalog, new AdmissionRunEffects(), new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        catalog.Publish(2);
        using var collector = new ActivityCollector(source => source.Name == AgentKitDiagnostics.ActivitySourceName, observation => observation.OperationName == AgentKitActivityNames.AgentAdmission && Equals(observation.GetTagItem(AgentKitTagNames.AgentId), definition.Id.ToString()));
        _ = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        var activity = collector.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("rejected");
        activity.GetTagItem(AgentKitTagNames.AgentCatalogVersion).ShouldBe("2");
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenPinnedRevisionIsReplaced_RejectsWithoutSilentlyUpgrading()
    {
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        catalog.Publish(2, CompositionTestData.Definition(revision: 2));
        var rejected = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        rejected.AgentId.ShouldBe(definition.Id);
        effects.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenSameRevisionContentChanges_RejectsBeforeLoopWork()
    {
        var definition = CompositionTestData.Definition();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        catalog.Publish(2, CompositionTestData.Definition(displayName: "changed agent"));
        _ = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        effects.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCatalogChangesOnlyForAnotherAgent_AdmitsPinnedDefinition()
    {
        var definition = CompositionTestData.Definition();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        catalog.Publish(2, definition, CompositionTestData.Definition(new AgentId(Guid.NewGuid()), "other agent"));
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
        effects.Requests.ShouldHaveSingleItem().AgentId.ShouldBe(definition.Id);
    }

    [Fact]
    public async Task RunAsync_WhenReloadReconstructsEquivalentNestedArrays_AdmitsPinnedDefinition()
    {
        var definition = DefinitionWithInstruction(includeTool: true);
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        var reconstructed = new AgentDefinition(
            definition.Id,
            definition.Revision,
            definition.DisplayName,
            new ModelSelectionPolicy(
                [.. definition.Models.Candidates],
                definition.Models.Fallback,
                definition.Models.Downgrade,
                new ExtensionData([.. definition.Models.Extensions.Values])),
            definition.ModelRequirements,
            new AgentInstructionSources([.. definition.InstructionSources]),
            [.. definition.Tools.Select(CloneTool)],
            definition.ToolChoice,
            definition.Settings,
            definition.RunDefaults,
            new ExtensionData([.. definition.Extensions.Values]),
            definition.SecurityProfile,
            definition.SessionProfile);
        catalog.Publish(2, reconstructed);
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
        definition.ShouldBe(reconstructed);
        definition.GetHashCode().ShouldBe(reconstructed.GetHashCode());
        effects.Requests.ShouldHaveSingleItem().Instructions.ShouldBe(reconstructed.Instructions, ignoreOrder: false);
    }

    [Fact]
    public async Task RunAsync_WhenLoopFailsAfterAdmission_EmitsOnlyOneAdmittedOutcome()
    {
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var effects = new AdmissionRunEffects
        {
            LoopException = new InvalidOperationException("loop failed")
        };
        await using var engine = Build(new MutableAgentDefinitionCatalog(definition), effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        using var activities = AdmissionActivities(definition.Id);
        using var metrics = new AdmissionMetricCollector();
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken));
        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("admitted");
        metrics.Snapshot().ShouldBe(["admitted"], ignoreOrder: false);
    }

    [Fact]
    public async Task RunAsync_WhenLoopCancelsAfterAdmission_EmitsOnlyOneAdmittedOutcome()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var effects = new AdmissionRunEffects
        {
            LoopException = new OperationCanceledException(cancellation.Token)
        };
        await using var engine = Build(new MutableAgentDefinitionCatalog(definition), effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        using var activities = AdmissionActivities(definition.Id);
        using var metrics = new AdmissionMetricCollector();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken));
        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("admitted");
        metrics.Snapshot().ShouldBe(["admitted"], ignoreOrder: false);
    }

    [Fact]
    public async Task RunAsync_WhenScopeFactoryThrowsAfterRunStartCapture_FailsWithoutLoopWork()
    {
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var runIds = new CountingRunIdGenerator();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(catalog));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, (_, _) => new ScopedRecordingAgentLoop(new AdmissionRunEffects()));
        CompositionTestData.AddRunServicesFakes(builder.Services);
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        await using var successfullyBuilt = builder.Build();
        await using var provider = builder.Services.BuildServiceProvider();
        await using var engine = new AgentEngine(new AgentEngineRuntime(new ThrowingScopeServiceProvider(provider), ownedProvider: null, new AgentCompositionSnapshot(new AgentRunProfilePublicationSnapshot([CompositionTestData.RunProfile(definition)]), successfullyBuilt.ComponentRegistrations)));
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        using var activities = AdmissionActivities(definition.Id);
        using var metrics = new AdmissionMetricCollector();
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken));
        runIds.Created.ShouldBe(0);
        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        metrics.Snapshot().ShouldBe(["failed"], ignoreOrder: false);
    }

    [Fact]
    public async Task RunAsync_WhenAdmissionFailsUnexpectedlyWithAnEnabledLogger_LogsFailedEventWithSafeFields()
    {
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var runIds = new CountingRunIdGenerator();
        var logger = new RecordingLogger<AgentEngine>();
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(catalog));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, (_, _) => new ScopedRecordingAgentLoop(new AdmissionRunEffects()));
        CompositionTestData.AddRunServicesFakes(builder.Services);
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        _ = builder.Services.AddSingleton<ILogger<AgentEngine>>(logger);
        await using var successfullyBuilt = builder.Build();
        await using var provider = builder.Services.BuildServiceProvider();
        await using var engine = new AgentEngine(new AgentEngineRuntime(new ThrowingScopeServiceProvider(provider), ownedProvider: null, new AgentCompositionSnapshot(new AgentRunProfilePublicationSnapshot([CompositionTestData.RunProfile(definition)]), successfullyBuilt.ComponentRegistrations)));
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken));

        var failedEntry = logger.Snapshot().Where(entry => entry.EventId.Id == 18002).ShouldHaveSingleItem();
        failedEntry.Level.ShouldBe(LogLevel.Error);
        failedEntry.Message.ShouldContain(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task RunAsync_WhenAmbientActivityExists_PreservesItAsAdmissionParent()
    {
        using var parentSource = new ActivitySource("admission-parent-test");
        using var parentListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == parentSource.Name,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(parentListener);
        using var parent = parentSource.StartActivity("parent")!;
        var definition = CompositionTestData.Definition();
        await using var engine = Build(new MutableAgentDefinitionCatalog(definition), new AdmissionRunEffects(), new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => observed = activity,
        };
        ActivitySource.AddActivityListener(listener);
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
        observed!.ParentSpanId.ShouldBe(parent.SpanId);
    }

    [Fact]
    public async Task RunAsync_WhenDiagnosticsThrow_PreservesAcceptRejectAndCancellationSemantics()
    {
        using var stoppedListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = static _ => throw new InvalidOperationException("Hostile stopped callback."),
        };
        ActivitySource.AddActivityListener(stoppedListener);
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>(static (_, _, _, _) => throw new InvalidOperationException("Hostile meter."));
        meterListener.Start();
        var definition = CompositionTestData.Definition();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        await using var engine = Build(catalog, new AdmissionRunEffects(), new CountingRunIdGenerator(), new ThrowingAgentEngineLogger());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
        meterListener.Dispose();
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
        using var samplingListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => throw new InvalidOperationException("Hostile sampler."),
        };
        ActivitySource.AddActivityListener(samplingListener);
        catalog.Publish(2);
        _ = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_WhenNestedToolContentChangesAtSameRevision_RejectsPinnedDefinition()
    {
        var definition = DefinitionWithInstruction(includeTool: true);
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        var changedTool = new LlmToolDefinition(definition.Tools[0].Id, definition.Tools[0].Name, "changed description", definition.Tools[0].ParametersSchema);
        var changed = new AgentDefinition(definition.Id, definition.Revision, definition.DisplayName, definition.Models, definition.ModelRequirements, definition.Instructions, [changedTool], definition.ToolChoice, definition.Settings, definition.RunDefaults, definition.Extensions, definition.SecurityProfile, definition.SessionProfile);
        catalog.Publish(2, changed);
        _ = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        effects.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCanceledBeforeCatalogRead_DoesNotStartAdmission()
    {
        var definition = CompositionTestData.Definition();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        await using var engine = Build(catalog, new AdmissionRunEffects(), new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var baselineReads = catalog.Reads;
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: cancellation.Token));
        catalog.Reads.ShouldBe(baselineReads);
    }

    [Fact]
    public async Task SendAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var loop = new GatedAgentLoop();
        await using var engine = CompositionTestData.SendableBuilder(loop, new InMemoryTestSessionCoordinator()).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => agent.SendAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SendAsync_WhenNoSessionIsNamed_CreatesOneAppendsTheUserMessageAndRunsTheLoopAgainstIt()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();

        var result = await agent.SendAsync(new AgentSendRequest(identity, "hello"), TestContext.Current.CancellationToken);

        var create = sessions.CreateRequests.ShouldHaveSingleItem();
        create.AgentId.ShouldBe(CompositionTestData.AgentId);
        create.Identity.ShouldBe(identity);
        create.Authorization.Scope.SessionId.ShouldBeNull();
        var entry = sessions.EntriesOf(result.SessionId).ShouldHaveSingleItem().ShouldBeOfType<MessageSessionEntry>();
        var user = entry.Message.ShouldBeOfType<UserMessage>();
        user.RunId.ShouldBe(result.RunId);
        user.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("hello");
        entry.Sequence.ShouldBe(new SessionSequence(1));
        var request = loop.Requests.ShouldHaveSingleItem();
        request.SessionId.ShouldBe(result.SessionId);
        request.BranchId.ShouldBe(result.BranchId);
        request.RunId.ShouldBe(result.RunId);
        request.Identity.ShouldBe(identity);
        request.Authorization.Scope.SessionId.ShouldBe(result.SessionId);
        request.Authorization.Scope.Correlation.ShouldBeOfType<InRunOperationCorrelation>().RunId.ShouldBe(result.RunId);
        _ = result.Outcome.ShouldBeOfType<RunSucceeded>();
    }

    [Fact]
    public async Task SendAsync_WhenSessionBackedInputQueueAndInputCoordinatorAreComposed_ResolvesInputCoordinatorInsideTheRunScope()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        var builder = CompositionTestData.SendableBuilder(loop, sessions);
        _ = builder.Services.AddSessionBackedInputQueue();
        _ = builder.Services.AddInputCoordinator();
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();

        _ = await agent.SendAsync(new AgentSendRequest(identity, "hello"), TestContext.Current.CancellationToken);

        var services = loop.Services.ShouldHaveSingleItem();
        _ = services.Input.ShouldNotBeNull();
    }

    [Fact]
    public async Task SendAsync_WhenASessionIsNamed_ContinuesItWithTheNextSequenceAndNoCreate()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var first = await agent.SendAsync(new AgentSendRequest(identity, "first"), TestContext.Current.CancellationToken);

        var second = await agent.SendAsync(new AgentSendRequest(identity, "second", first.SessionId), TestContext.Current.CancellationToken);

        second.SessionId.ShouldBe(first.SessionId);
        second.BranchId.ShouldBe(first.BranchId);
        second.RunId.ShouldNotBe(first.RunId);
        sessions.CreateRequests.Count.ShouldBe(1);
        var entries = sessions.EntriesOf(first.SessionId);
        entries.Length.ShouldBe(2);
        entries[1].Sequence.ShouldBe(new SessionSequence(2));
        loop.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_WhenTheNamedSessionBelongsToAnotherPrincipal_RejectsWithoutAppending()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        var foreign = new SessionId(Guid.NewGuid());
        sessions.Seed(new SessionDescriptor(
            new SessionAddress(CompositionTestData.AgentId, foreign), null, new TenantId("tenant"), new PrincipalId("someone-else"),
            new SessionStoreKey("store"), new BranchId(Guid.NewGuid()), new SessionVersion(0), SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), ExtensionData.Empty));
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        var exception = await Should.ThrowAsync<AgentAdmissionRejectedException>(() =>
            agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi", foreign), TestContext.Current.CancellationToken));

        exception.Rejection.Reason.ShouldContain("not visible");
        sessions.AppendRequests.ShouldBeEmpty();
        loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenTheNamedSessionDoesNotExist_RejectsWithoutAppending()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(() =>
            agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi", new SessionId(Guid.NewGuid())), TestContext.Current.CancellationToken));

        sessions.AppendRequests.ShouldBeEmpty();
        loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenRunAcceptanceFails_RejectsWithoutRunningTheLoop()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator { AcceptRunOverride = new SessionRunStartRejected("store fault") };
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        var exception = await Should.ThrowAsync<AgentAdmissionRejectedException>(() =>
            agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi"), TestContext.Current.CancellationToken));

        exception.Rejection.Reason.ShouldContain("could not be accepted");
        loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenTwoTurnsRaceForOneSessionAndTheProfileRejects_TheLoserIsRejectedWithoutAppending()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource() };
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var opener = await SeedSessionAsync(agent, identity, loop);
        loop.Gate = new TaskCompletionSource();

        var running = agent.SendAsync(new AgentSendRequest(identity, "one", opener), TestContext.Current.CancellationToken);
        await loop.Entered.Task;
        var before = sessions.EntriesOf(opener).Length;
        var rejected = await agent.RunAsync<string>(
            opener,
            identity,
            new AgentInput(
                new InputId(Guid.NewGuid()),
                InputDelivery.Steer,
                [new TextPart("two", TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty),
            cancellationToken: TestContext.Current.CancellationToken);
        loop.Gate.SetResult();
        _ = await running;

        var failure = rejected.ShouldBeOfType<AgentRunRejected<string>>();
        failure.AgentId.ShouldBe(CompositionTestData.AgentId);
        failure.SessionId.ShouldBe(opener);
        failure.Failure.Code.ShouldBe(AgentErrorCodes.SessionBusy);
        failure.Failure.IsRetryable.ShouldBeTrue();
        sessions.EntriesOf(opener).Length.ShouldBe(before);
        loop.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_WhenTwoTurnsRaceForOneSessionAndTheProfileWaits_TheyRunOneAfterTheOther()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions, SessionBusyBehavior.Wait).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var opener = await SeedSessionAsync(agent, identity, loop);
        loop.Gate = new TaskCompletionSource();

        var first = agent.SendAsync(new AgentSendRequest(identity, "one", opener), TestContext.Current.CancellationToken);
        await loop.Entered.Task;
        var second = agent.SendAsync(new AgentSendRequest(identity, "two", opener), TestContext.Current.CancellationToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        second.IsCompleted.ShouldBeFalse();
        loop.Requests.Count.ShouldBe(2);
        loop.Gate.SetResult();
        _ = await first;
        _ = await second;

        loop.PeakConcurrency.ShouldBe(1);
        sessions.EntriesOf(opener).OfType<MessageSessionEntry>().Select(static e => e.Sequence.Value).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task SendAsync_WhenTurnsTargetDifferentSessions_RunConcurrently()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource() };
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();

        var first = agent.SendAsync(new AgentSendRequest(identity, "a"), TestContext.Current.CancellationToken);
        var second = agent.SendAsync(new AgentSendRequest(identity, "b"), TestContext.Current.CancellationToken);
        while (loop.Requests.Count < 2)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        loop.Gate.SetResult();
        var results = await Task.WhenAll(first, second);

        loop.PeakConcurrency.ShouldBe(2);
        results[0].SessionId.ShouldNotBe(results[1].SessionId);
    }

    [Fact]
    public async Task SteerAsync_WhenARunHoldsTheLane_AdmitsSteeringWithoutAppendingHistory()
    {
        var loop = new GatedAgentLoop { Gate = new TaskCompletionSource() };
        var sessions = new InMemoryTestSessionCoordinator();
        var builder = CompositionTestData.SendableBuilder(loop, sessions);
        _ = builder.Services.AddInputCoordinator().AddSessionBackedInputQueue();
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var opener = await SeedSessionAsync(agent, identity, loop);
        loop.Gate = new TaskCompletionSource();
        var running = agent.SendAsync(new AgentSendRequest(identity, "one", opener), TestContext.Current.CancellationToken);
        await loop.Entered.Task;
        var before = sessions.EntriesOf(opener).Length;
        var input = QueueInput(InputDelivery.Steer, "steer-me");

        var admitted = await agent.SteerAsync(opener, identity, input, cancellationToken: TestContext.Current.CancellationToken);

        var receipt = admitted.ShouldBeOfType<AcceptedInput>().Receipt;
        receipt.InputId.ShouldBe(input.Id);
        receipt.SessionId.ShouldBe(opener);
        receipt.Existing.ShouldBeFalse();
        sessions.EntriesOf(opener).Length.ShouldBe(before);
        loop.Gate.SetResult();
        _ = await running;
    }

    [Fact]
    public async Task FollowUpAsync_WhenThePreviousRunHasSettled_AdmitsFollowUp()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        var builder = CompositionTestData.SendableBuilder(loop, sessions);
        _ = builder.Services.AddInputCoordinator().AddSessionBackedInputQueue();
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();
        var opener = await SeedSessionAsync(agent, identity, loop);
        var before = sessions.EntriesOf(opener).Length;
        var input = QueueInput(InputDelivery.FollowUp, "later");

        var admitted = await agent.FollowUpAsync(opener, identity, input, cancellationToken: TestContext.Current.CancellationToken);

        var receipt = admitted.ShouldBeOfType<AcceptedInput>().Receipt;
        receipt.InputId.ShouldBe(input.Id);
        receipt.Existing.ShouldBeFalse();
        sessions.EntriesOf(opener).Length.ShouldBe(before);
    }

    [Fact]
    public async Task SteerAsync_WhenInputIsFollowUp_ThrowsBeforeAdmission()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var opener = await SeedSessionAsync(agent, CompositionTestData.Identity(), loop);

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            agent.SteerAsync(
                opener,
                CompositionTestData.Identity(),
                QueueInput(InputDelivery.FollowUp, "not-steering"),
                cancellationToken: TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("input");
    }

    private static AgentInput QueueInput(InputDelivery delivery, string text) =>
        new(
            new InputId(Guid.NewGuid()),
            delivery,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    [Fact]
    public async Task SendAsync_WhenTwoAgentsShareTheEngine_EachRunsItsOwnDefinition()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        var other = new AgentId(Guid.Parse("a0000000-0000-0000-0000-00000000000b"));
        await using var engine = CompositionTestData.SendableBuilder(
            loop, sessions, SessionBusyBehavior.Reject,
            CompositionTestData.Definition(), CompositionTestData.Definition(other, "other agent", maxTurns: 3)).Build();
        var first = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var second = (await engine.GetAgentAsync(other, TestContext.Current.CancellationToken)).RequireResolved();
        var identity = CompositionTestData.Identity();

        var firstResult = await first.SendAsync(new AgentSendRequest(identity, "one"), TestContext.Current.CancellationToken);
        var secondResult = await second.SendAsync(new AgentSendRequest(identity, "two"), TestContext.Current.CancellationToken);

        firstResult.AgentId.ShouldBe(CompositionTestData.AgentId);
        secondResult.AgentId.ShouldBe(other);
        firstResult.SessionId.ShouldNotBe(secondResult.SessionId);
        loop.Requests[0].MaxTurns.ShouldBe(8);
        loop.Requests[1].MaxTurns.ShouldBe(3);
        sessions.CreateRequests.Select(static c => c.AgentId).ShouldBe([CompositionTestData.AgentId, other]);
    }

    [Fact]
    public async Task SendAsync_WhenAnObserverIsSupplied_ItReceivesTheLoopsProgress()
    {
        var loop = new GatedAgentLoop();
        await using var engine = CompositionTestData.SendableBuilder(loop, new InMemoryTestSessionCoordinator()).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        var observer = new RecordingRunObserver();

        _ = await agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi", observer: observer), TestContext.Current.CancellationToken);

        _ = observer.Events.ShouldHaveSingleItem().ShouldBeOfType<AgentRunModelResponseEvent>();
        loop.Requests.Single().Observer.ShouldBeSameAs(observer);
    }

    [Fact]
    public async Task SendAsync_WhenAnOverrideWidensTheDefinition_ThrowsArgumentOutOfRangeExceptionBeforeAnyEffect()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi", maxTurns: 99), TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
        sessions.CreateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenAnOverrideNarrowsTheDefinition_UsesIt()
    {
        var loop = new GatedAgentLoop();
        await using var engine = CompositionTestData.SendableBuilder(loop, new InMemoryTestSessionCoordinator()).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi", maxTurns: 2, attemptTimeout: TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);

        loop.Requests.Single().MaxTurns.ShouldBe(2);
        loop.Requests.Single().AttemptTimeout.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SendAsync_WhenCancelledBeforeTheMessageIsCommitted_PropagatesAndLeavesNoRun()
    {
        var loop = new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator { AcceptRunGate = new TaskCompletionSource() };
        await using var engine = CompositionTestData.SendableBuilder(loop, sessions).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        using var cancellation = new CancellationTokenSource();

        var pending = agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi"), cancellation.Token);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
        loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenTheEngineIsDisposed_ThrowsObjectDisposedException()
    {
        var loop = new GatedAgentLoop();
        var engine = CompositionTestData.SendableBuilder(loop, new InMemoryTestSessionCoordinator()).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();
        await engine.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(() =>
            agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_WhenTheDefinitionSelectsAnOutput_CarriesItOnTheRequest()
    {
        var loop = new GatedAgentLoop();
        var output = new OutputDefinition(
            new OutputDefinitionId("answer"), new OutputDefinitionVersion("1"), "answer", OutputMode.Text,
            schema: null, runtimeType: null, alternatives: [], validators: [],
            OutputValidationPolicy.RejectOnFirstFailure, OutputRetryPolicy.None, OutputEndStrategy.Graceful);
        var definition = CompositionTestData.Definition() with { Output = output };
        await using var engine = CompositionTestData.SendableBuilder(loop, new InMemoryTestSessionCoordinator(), SessionBusyBehavior.Reject, definition).Build();
        var agent = (await engine.GetAgentAsync(CompositionTestData.AgentId, TestContext.Current.CancellationToken)).RequireResolved();

        _ = await agent.SendAsync(new AgentSendRequest(CompositionTestData.Identity(), "hi"), TestContext.Current.CancellationToken);

        loop.Requests.Single().Output.ShouldBe(output);
    }

    private static async Task<SessionId> SeedSessionAsync(Agent agent, ExecutionIdentity identity, GatedAgentLoop loop)
    {
        var gate = loop.Gate;
        loop.Gate = null;
        var result = await agent.SendAsync(new AgentSendRequest(identity, "seed"), TestContext.Current.CancellationToken);
        loop.Gate = gate;
        return result.SessionId;
    }

    private sealed class RecordingRunObserver: IAgentRunObserver
    {
        public List<AgentRunEvent> Events { get; } = [];

        public ValueTask OnEventAsync(AgentRunEvent runEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(runEvent);
            return ValueTask.CompletedTask;
        }
    }

    private static AgentEngine Build(MutableAgentDefinitionCatalog catalog, AdmissionRunEffects effects, CountingRunIdGenerator runIds, ILogger<AgentEngine>? logger = null)
    {
        var builder = AgentEngine.CreateBuilder();
        var sessions = new InMemoryTestSessionCoordinator();
        _ = builder.Services.AddSingleton<ISessionCoordinator>(sessions);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(catalog));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        _ = builder.Services.AddKeyedScoped<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, (_, _) => new ScopedRecordingAgentLoop(effects));
        CompositionTestData.AddRunServicesFakes(builder.Services);
        var snapshot = catalog.CurrentSnapshot ?? throw new InvalidOperationException("The test catalog must be ready before engine construction.");
        foreach (var definition in snapshot.Definitions)
        {
            CompositionTestData.SeedSession(sessions, definition.Id, CompositionTestData.SessionId);
        }
        CompositionTestData.AddRunProfiles(builder.Services, [.. snapshot.Definitions]);
        if (logger is not null)
        {
            _ = builder.Services.AddSingleton(logger);
        }

        return builder.Build();
    }

    private static ActivityCollector AdmissionActivities(AgentId agentId) => new(source => source.Name == AgentKitDiagnostics.ActivitySourceName, observation => observation.OperationName == AgentKitActivityNames.AgentAdmission && Equals(observation.GetTagItem(AgentKitTagNames.AgentId), agentId.ToString()));
    private static AgentDefinition DefinitionWithInstruction(bool includeTool = false) => new(CompositionTestData.AgentId, new AgentDefinitionRevision(1), "test agent", new ModelSelectionPolicy([new ModelAlias("chat")]), ModelRequirements.None, [new SystemMessage(new MessageId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), CompositionTestData.AgentId, CompositionTestData.SessionId, conversationId: null, CompositionTestData.BranchId, runId: null, turnId: null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("keep this", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty)], tools: includeTool ? [Tool()] : [], LlmToolChoice.Auto, LlmRequestSettings.Default, new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)), ExtensionData.Empty, new SecurityProfileKey("security"), new SessionProfileKey("session"));
    private static LlmToolDefinition CloneTool(LlmToolDefinition tool) => new(tool.Id, tool.Name, tool.Description, ParseSchema(tool.ParametersSchema.GetRawText()));
    private static LlmToolDefinition Tool() => new(new ToolId("test-tool"), "test_tool", "A test tool.", ParseSchema( /*lang=json,strict*/"{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}}}"));
    private static JsonElement ParseSchema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task RunAsync_WhenRuntimePublicationDiffersFromPinnedSnapshot_RejectsBeforeIdentifiersOrLoop()
    {
        var definition = CompositionTestData.Definition();
        var pinned = CompositionTestData.RunProfile(definition);
        var changed = new AgentRunProfilePublication(new SecurityProfilePublication(definition.Id, definition.Revision, new ConfigurationVersion(2), pinned.SecurityProfile.ProfileKey, pinned.SecurityProfile.ProfileVersion, pinned.SecurityProfile.PolicySnapshot, pinned.SecurityProfile.AuthorityKey), pinned.SessionProfile);
        var reader = new MutableRunProfilePublicationReader(new AgentRunProfilePublicationSnapshot([pinned]), new AgentRunProfilePublicationFound(pinned));
        var selector = new TestSecurityProfileSelector();
        var runIds = new CountingRunIdGenerator();
        var loop = new RecordingAgentLoop();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, loop);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<ISecurityProfileSelector>(selector));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentRunProfilePublicationReader>(reader));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        CompositionTestData.AddRunServicesFakes(builder.Services);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        reader.Result = new AgentRunProfilePublicationFound(changed);
        _ = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        reader.Reads.ShouldBe(1);
        runIds.Created.ShouldBe(0);
        selector.Requests.ShouldBeEmpty();
        loop.ReceivedRequests.ShouldBeEmpty();
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
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        _ = await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken);
        var capture = selector.Requests.Last();
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
        _ = builder.Services.Replace(ServiceDescriptor.KeyedScoped<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, (_, _) => new ScopedRecordingAgentLoop(effects)));
        await using var engine = builder.Build();
        var baselineScopes = effects.Scopes;
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        _ = (await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeOfType<AgentRunRejected<string>>();
        _ = selector.Requests.ShouldHaveSingleItem();
        effects.Scopes.ShouldBe(baselineScopes);
        effects.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_WhenRunProfileReadCancelsBeforeReturning_PropagatesBeforeDependentWork(bool returnsFound)
    {
        var definition = CompositionTestData.Definition();
        var publication = CompositionTestData.RunProfile(definition);
        using var cancellation = new CancellationTokenSource();
        var reader = new CancellingRunProfilePublicationReader(new AgentRunProfilePublicationSnapshot([publication]), returnsFound ? new AgentRunProfilePublicationFound(publication) : new AgentRunProfilePublicationUnavailable("Cancelled read returned unavailable."), cancellation);
        var selector = new TestSecurityProfileSelector();
        var runIds = new CountingRunIdGenerator();
        var loop = new RecordingAgentLoop();
        var builder = AgentEngine.CreateBuilder();
        CompositionTestData.AddRequiredSecurityGrantStore(builder.Services);
        _ = builder.Services.AddAgent(definition);
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, loop);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<ISecurityProfileSelector>(selector));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentRunProfilePublicationReader>(reader));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        CompositionTestData.AddRunServicesFakes(builder.Services);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: cancellation.Token));
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
        var selector = new TestSecurityProfileSelector
        {
            CancellationSource = cancellation
        };
        var effects = new AdmissionRunEffects();
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<ISecurityProfileSelector>(selector));
        _ = builder.Services.Replace(ServiceDescriptor.KeyedScoped<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, (_, _) => new ScopedRecordingAgentLoop(effects)));
        await using var engine = builder.Build();
        var baselineScopes = effects.Scopes;
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).RequireResolved();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await agent.RunAsync<string>(CompositionTestData.SessionId, CompositionTestData.Identity(), CompositionTestData.Input(), options: CompositionTestData.RunOptions(), cancellationToken: cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        _ = selector.Requests.ShouldHaveSingleItem();
        effects.Scopes.ShouldBe(baselineScopes);
        effects.Requests.ShouldBeEmpty();
    }
}
