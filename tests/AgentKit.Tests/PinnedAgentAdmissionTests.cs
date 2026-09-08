// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

using AgentKit.Observability;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

[Collection(AdmissionObservabilityGroup.Name)]
public sealed class PinnedAgentAdmissionTests
{
    [Fact]
    public async Task RunAsync_WhenPinnedDefinitionIsRemoved_RejectsBeforeAllocatingRunOrScope()
    {
        var definition = DefinitionWithInstruction();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        var runIds = new CountingRunIdGenerator();
        await using var engine = Build(catalog, effects, runIds);
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        var baselineScopes = effects.Scopes;

        catalog.Publish(2);

        var exception = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        exception.Rejection.AgentId.ShouldBe(definition.Id);
        exception.Rejection.PinnedRevision.ShouldBe(definition.Revision);
        exception.Rejection.CatalogVersion.ShouldBe(new AgentCatalogVersion(2));
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
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        catalog.Publish(2);
        using var collector = new ActivityCollector(
            source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.AgentAdmission
                && Equals(observation.GetTagItem(AgentKitTagNames.AgentId), definition.Id.ToString()));

        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

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
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        catalog.Publish(2, CompositionTestData.Definition(revision: 2));

        var exception = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        exception.Rejection.CatalogVersion.ShouldBe(new AgentCatalogVersion(2));
        effects.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenSameRevisionContentChanges_RejectsBeforeLoopWork()
    {
        var definition = CompositionTestData.Definition();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        catalog.Publish(2, CompositionTestData.Definition(displayName: "changed agent"));

        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        effects.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCatalogChangesOnlyForAnotherAgent_AdmitsPinnedDefinition()
    {
        var definition = CompositionTestData.Definition();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        catalog.Publish(2, definition, CompositionTestData.Definition(new AgentId(Guid.NewGuid()), "other agent"));

        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);

        effects.Requests.ShouldHaveSingleItem().AgentId.ShouldBe(definition.Id);
    }

    [Fact]
    public async Task RunAsync_WhenReloadReconstructsEquivalentNestedArrays_AdmitsPinnedDefinition()
    {
        var definition = DefinitionWithInstruction(includeTool: true);
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
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
            [.. definition.Instructions.Select(CloneMessage)],
            [.. definition.Tools.Select(CloneTool)],
            definition.ToolChoice,
            definition.Settings,
            definition.RunDefaults,
            new ExtensionData([.. definition.Extensions.Values]),
            definition.SecurityProfile,
            definition.SessionProfile);

        catalog.Publish(2, reconstructed);

        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);

        definition.ShouldBe(reconstructed);
        definition.GetHashCode().ShouldBe(reconstructed.GetHashCode());
        effects.Requests.ShouldHaveSingleItem().Instructions.ShouldBe(reconstructed.Instructions, ignoreOrder: false);
    }

    [Fact]
    public async Task RunAsync_WhenLoopFailsAfterAdmission_EmitsOnlyOneAdmittedOutcome()
    {
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var effects = new AdmissionRunEffects { LoopException = new InvalidOperationException("loop failed") };
        await using var engine = Build(new MutableAgentDefinitionCatalog(definition), effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        using var activities = AdmissionActivities(definition.Id);
        using var metrics = new AdmissionMetricCollector();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("admitted");
        metrics.Snapshot().ShouldBe(["admitted"], ignoreOrder: false);
    }

    [Fact]
    public async Task RunAsync_WhenLoopCancelsAfterAdmission_EmitsOnlyOneAdmittedOutcome()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var definition = CompositionTestData.Definition(new AgentId(Guid.NewGuid()));
        var effects = new AdmissionRunEffects { LoopException = new OperationCanceledException(cancellation.Token) };
        await using var engine = Build(new MutableAgentDefinitionCatalog(definition), effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        using var activities = AdmissionActivities(definition.Id);
        using var metrics = new AdmissionMetricCollector();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

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
        _ = builder.Services.AddScoped<IAgentLoop>(_ => new ScopedRecordingAgentLoop(new AdmissionRunEffects()));
        CompositionTestData.AddRunProfiles(builder.Services, definition);
        await using var successfullyBuilt = builder.Build();
        await using var provider = builder.Services.BuildServiceProvider();
        await using var engine = new AgentEngine(
            new ThrowingScopeServiceProvider(provider),
            ownedProvider: null,
            new AgentRunProfilePublicationSnapshot([CompositionTestData.RunProfile(definition)]));
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        using var activities = AdmissionActivities(definition.Id);
        using var metrics = new AdmissionMetricCollector();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        runIds.Created.ShouldBe(1);
        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        metrics.Snapshot().ShouldBe(["failed"], ignoreOrder: false);
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
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => observed = activity,
        };
        ActivitySource.AddActivityListener(listener);

        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);

        observed!.ParentSpanId.ShouldBe(parent.SpanId);
    }

    [Fact]
    public void LlmToolDefinition_WhenSourceDocumentIsDisposed_RetainsConstructorAndInitializerSchemas()
    {
        LlmToolDefinition constructed;
        LlmToolDefinition initialized;
        using (var document = JsonDocument.Parse("{\"type\":\"object\"}"))
        {
            constructed = new LlmToolDefinition(new ToolId("constructed"), "constructed", null, document.RootElement);
            initialized = new LlmToolDefinition(new ToolId("initialized"), "initialized", null, default)
            {
                ParametersSchema = document.RootElement,
            };
        }

        constructed.ParametersSchema.GetProperty("type").GetString().ShouldBe("object");
        initialized.ParametersSchema.GetProperty("type").GetString().ShouldBe("object");
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
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;

        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);
        meterListener.Dispose();
        _ = await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken);
        using var samplingListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => throw new InvalidOperationException("Hostile sampler."),
        };
        ActivitySource.AddActivityListener(samplingListener);
        catalog.Publish(2);
        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_WhenNestedToolContentChangesAtSameRevision_RejectsPinnedDefinition()
    {
        var definition = DefinitionWithInstruction(includeTool: true);
        var catalog = new MutableAgentDefinitionCatalog(definition);
        var effects = new AdmissionRunEffects();
        await using var engine = Build(catalog, effects, new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        var changedTool = new LlmToolDefinition(
            definition.Tools[0].Id,
            definition.Tools[0].Name,
            "changed description",
            definition.Tools[0].ParametersSchema);
        var changed = new AgentDefinition(
            definition.Id,
            definition.Revision,
            definition.DisplayName,
            definition.Models,
            definition.ModelRequirements,
            definition.Instructions,
            [changedTool],
            definition.ToolChoice,
            definition.Settings,
            definition.RunDefaults,
            definition.Extensions,
            definition.SecurityProfile,
            definition.SessionProfile);

        catalog.Publish(2, changed);

        _ = await Should.ThrowAsync<AgentAdmissionRejectedException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), TestContext.Current.CancellationToken));

        effects.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCanceledBeforeCatalogRead_DoesNotStartAdmission()
    {
        var definition = CompositionTestData.Definition();
        var catalog = new MutableAgentDefinitionCatalog(definition);
        await using var engine = Build(catalog, new AdmissionRunEffects(), new CountingRunIdGenerator());
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken))!;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var baselineReads = catalog.Reads;

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await agent.RunAsync(CompositionTestData.RunOptions(), cancellation.Token));

        catalog.Reads.ShouldBe(baselineReads);
    }

    [Fact]
    public void AgentAdmissionRejection_WhenArgumentsAreInvalid_ThrowsWithArgumentNames()
    {
        var defaultIdentity = Should.Throw<ArgumentOutOfRangeException>(
            () => new AgentAdmissionRejection(default, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), "removed"));
        var nullReason = Should.Throw<ArgumentNullException>(
            () => new AgentAdmissionRejection(CompositionTestData.AgentId, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), null!));
        var blankReason = Should.Throw<ArgumentException>(
            () => new AgentAdmissionRejection(CompositionTestData.AgentId, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), " "));

        defaultIdentity.ParamName.ShouldBe("agentId");
        nullReason.ParamName.ShouldBe("reason");
        blankReason.ParamName.ShouldBe("reason");
    }

    [Fact]
    public void AgentAdmissionRejectedException_WhenRejectionIsNull_ThrowsBeforeConstruction()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AgentAdmissionRejectedException(null!));

        exception.ParamName.ShouldBe("rejection");
    }

    [Fact]
    public void AgentAdmissionRejectedException_WhenRejectionIsValid_PreservesItsEvidence()
    {
        var rejection = new AgentAdmissionRejection(
            CompositionTestData.AgentId,
            new AgentDefinitionRevision(1),
            new AgentCatalogVersion(2),
            "removed");

        var exception = new AgentAdmissionRejectedException(rejection);

        exception.Rejection.ShouldBeSameAs(rejection);
        exception.Message.ShouldBe("removed");
    }

    [Fact]
    public void LlmToolDefinition_WhenSchemasHaveEquivalentJsonContent_UsesEqualHashCodes()
    {
        var first = new LlmToolDefinition(
            new ToolId("test-tool"),
            "test_tool",
            "A test tool.",
            ParseSchema(/*lang=json,strict*/ "{ \"name\": \"\\u0061\", \"count\": 1.0 }"));
        var second = new LlmToolDefinition(
            first.Id,
            first.Name,
            first.Description,
            ParseSchema(/*lang=json,strict*/ "{\"count\":1,\"name\":\"a\"}"));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void LlmToolDefinition_WhenSchemasAreUndefined_UsesValueEqualityWithoutThrowing()
    {
        var first = new LlmToolDefinition(
            new ToolId("test-tool"),
            "test_tool",
            "A test tool.",
            default);
        var second = new LlmToolDefinition(first.Id, first.Name, first.Description, default);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static AgentEngine Build(
        MutableAgentDefinitionCatalog catalog,
        AdmissionRunEffects effects,
        CountingRunIdGenerator runIds,
        ILogger<AgentEngine>? logger = null)
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IAgentDefinitionCatalog>(catalog));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<RunId>>(runIds));
        _ = builder.Services.AddScoped<IAgentLoop>(_ => new ScopedRecordingAgentLoop(effects));
        var snapshot = catalog.CurrentSnapshot
            ?? throw new InvalidOperationException("The test catalog must be ready before engine construction.");
        CompositionTestData.AddRunProfiles(
            builder.Services,
            [.. snapshot.Definitions]);
        if (logger is not null)
        {
            _ = builder.Services.AddSingleton(logger);
        }
        return builder.Build();
    }

    private static ActivityCollector AdmissionActivities(AgentId agentId) => new(
        source => source.Name == AgentKitDiagnostics.ActivitySourceName,
        observation => observation.OperationName == AgentKitActivityNames.AgentAdmission
            && Equals(observation.GetTagItem(AgentKitTagNames.AgentId), agentId.ToString()));

    private static AgentDefinition DefinitionWithInstruction(bool includeTool = false) => new(
        CompositionTestData.AgentId,
        new AgentDefinitionRevision(1),
        "test agent",
        new ModelSelectionPolicy([new ModelAlias("chat")]),
        ModelRequirements.None,
        [new SystemMessage(
            new MessageId(Guid.Parse("d0000000-0000-0000-0000-000000000004")),
            CompositionTestData.AgentId,
            CompositionTestData.SessionId,
            conversationId: null,
            CompositionTestData.BranchId,
            runId: null,
            turnId: null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("keep this", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty)],
        tools: includeTool ? [Tool()] : [],
        LlmToolChoice.Auto,
        LlmRequestSettings.Default,
        new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)),
        ExtensionData.Empty,
        new SecurityProfileKey("security"),
        new SessionProfileKey("session"));

    private static AgentMessage CloneMessage(AgentMessage message) => new SystemMessage(
        message.Id,
        message.AgentId,
        message.SessionId,
        message.ConversationId,
        message.BranchId,
        message.RunId,
        message.TurnId,
        message.CreatedAt,
        message.State,
        [.. message.Parts.Select(part => new TextPart(
            ((TextPart)part).Text,
            ((TextPart)part).Semantics,
            new ExtensionData([.. part.Extensions.Values])))],
        new ExtensionData([.. message.Extensions.Values]));

    private static LlmToolDefinition CloneTool(LlmToolDefinition tool) => new(
        tool.Id,
        tool.Name,
        tool.Description,
        ParseSchema(tool.ParametersSchema.GetRawText()));

    private static LlmToolDefinition Tool() => new(
        new ToolId("test-tool"),
        "test_tool",
        "A test tool.",
        ParseSchema(/*lang=json,strict*/ "{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}}}"));

    private static JsonElement ParseSchema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

}
