// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies DefaultModelSelector behavior and contracts.</summary>
public sealed class DefaultModelSelectorTests
{
    private readonly IModelSelector _selector = CreateSelector();
    [Fact]
    public async Task SelectAsync_WhenFirstCandidateMatches_SelectsIt()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("fast"), ProviderTestData.Model("smart"));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(candidates: ["fast", "smart"]));
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var selected = result.ShouldBeOfType<ModelSelected>();
        selected.Decision.Model.Alias.Value.ShouldBe("fast");
        selected.Decision.CatalogVersion.ShouldBe(catalog.Version);
    }

    [Fact]
    public async Task SelectAsync_RecordsTheCatalogVersionTheChoiceWasMadeAgainst()
    {
        var catalog = new ModelCatalogSnapshot(new ModelCatalogVersion(7), [ProviderTestData.Model("fast")]);
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(candidates: ["fast"]));
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ModelSelected>().Decision.CatalogVersion.Value.ShouldBe(7);
    }

    [Fact]
    public async Task SelectAsync_WhenFirstCandidateMissingAndFallbackDisabled_ReturnsNoCompatibleModel()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("smart"));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(candidates: ["fast", "smart"]));
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var none = result.ShouldBeOfType<NoCompatibleModel>();
        none.Diagnostics[0].Outcome.ShouldBe(ModelCandidateOutcome.NotInCatalog);
        none.Diagnostics[1].Outcome.ShouldBe(ModelCandidateOutcome.NotEvaluated);
    }

    [Fact]
    public async Task SelectAsync_WhenFirstCandidateMissingAndFallbackEnabled_SelectsNext()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("smart"));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(ModelFallbackPolicy.OrderedCandidates, candidates: ["fast", "smart"]));
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ModelSelected>().Decision.Model.Alias.Value.ShouldBe("smart");
    }

    [Fact]
    public async Task SelectAsync_WhenCandidateLacksRequiredCapability_SkipsItUnderFallback()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("no-tools", toolCalls: false), ProviderTestData.Model("tools", toolCalls: true));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(ModelFallbackPolicy.OrderedCandidates, candidates: ["no-tools", "tools"]), new ModelRequirements { RequiresToolCalls = true });
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var selected = result.ShouldBeOfType<ModelSelected>();
        selected.Decision.Model.Alias.Value.ShouldBe("tools");
        selected.Decision.Diagnostics[0].Outcome.ShouldBe(ModelCandidateOutcome.MissingRequiredCapability);
    }

    [Fact]
    public async Task SelectAsync_WhenOnlyContextWindowIsTooSmall_ReportsInsufficientContextWindow()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("small", maxContextTokens: 100));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(candidates: ["small"]), new ModelRequirements { MinimumInputTokens = 10_000 });
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NoCompatibleModel>().Diagnostics.ShouldHaveSingleItem().Outcome.ShouldBe(ModelCandidateOutcome.InsufficientContextWindow);
    }

    [Fact]
    public async Task SelectAsync_WhenNoCandidateMatches_ReportsEveryCandidate()
    {
        var catalog = ProviderTestData.Catalog();
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(ModelFallbackPolicy.OrderedCandidates, candidates: ["a", "b", "c"]));
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NoCompatibleModel>().Diagnostics.Length.ShouldBe(3);
    }

    [Fact]
    public async Task SelectAsync_WhenDowngradeAllowed_SelectsAndReportsAdjustmentCount()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("no-stream", streaming: false));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(downgrade: CapabilityDowngradePolicy.AllowDeclaredAdjustments, candidates: ["no-stream"]), new ModelRequirements { RequiresStreaming = true });
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var selected = result.ShouldBeOfType<ModelSelected>();
        selected.Decision.Diagnostics.ShouldHaveSingleItem().Reason.ShouldContain("adjustment");
    }

    [Fact]
    public async Task SelectAsync_MarksLaterCandidatesNotEvaluatedAfterASelection()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("a"), ProviderTestData.Model("b"));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(ModelFallbackPolicy.OrderedCandidates, candidates: ["a", "b"]));
        var result = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var diagnostics = result.ShouldBeOfType<ModelSelected>().Decision.Diagnostics;
        diagnostics[0].Outcome.ShouldBe(ModelCandidateOutcome.Selected);
        diagnostics[1].Outcome.ShouldBe(ModelCandidateOutcome.NotEvaluated);
    }

    [Fact]
    public async Task SelectAsync_IsDeterministicForTheSameInputs()
    {
        var catalog = ProviderTestData.Catalog(ProviderTestData.Model("a"), ProviderTestData.Model("b"));
        var request = ProviderTestData.SelectionRequest(catalog, ProviderTestData.Policy(ModelFallbackPolicy.OrderedCandidates, candidates: ["a", "b"]));
        var first = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var second = await _selector.SelectAsync(request, TestContext.Current.CancellationToken);
        first.ShouldBeOfType<ModelSelected>().Decision.Model.Alias.ShouldBe(second.ShouldBeOfType<ModelSelected>().Decision.Model.Alias);
    }

    [Fact]
    public async Task SelectAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await _selector.SelectAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SelectAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var request = ProviderTestData.SelectionRequest(ProviderTestData.Catalog(ProviderTestData.Model("a")), ProviderTestData.Policy(candidates: ["a"]));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await _selector.SelectAsync(request, cancellation.Token));
    }

    private static IModelSelector CreateSelector()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentProviders();
        return services.BuildServiceProvider().GetRequiredService<IModelSelector>();
    }

    [Fact]
    public async Task SelectAsync_WhenCapabilityValidatorThrows_PropagatesExceptionAfterRecordingFailure()
    {
        var logger = new RecordingLogger<DefaultModelSelector>();
        var selector = new DefaultModelSelector(new ThrowingCapabilityValidator(), logger);
        var request = ProviderTestData.SelectionRequest(ProviderTestData.Catalog(ProviderTestData.Model("a")), ProviderTestData.Policy(candidates: ["a"]));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await selector.SelectAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Validator exploded.");
        logger.Snapshot().ShouldContain(entry => entry.Message.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SelectAsync_WhenCancelled_LogsCancellation()
    {
        var logger = new RecordingLogger<DefaultModelSelector>();
        var selector = new DefaultModelSelector(new DefaultModelCapabilityValidator(), logger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var request = ProviderTestData.SelectionRequest(ProviderTestData.Catalog(ProviderTestData.Model("a")), ProviderTestData.Policy(candidates: ["a"]));

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await selector.SelectAsync(request, cancellation.Token));

        logger.Snapshot().ShouldContain(entry => entry.Message.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SelectAsync_WhenCandidateSelected_LogsModelSelected()
    {
        var logger = new RecordingLogger<DefaultModelSelector>();
        var selector = new DefaultModelSelector(new DefaultModelCapabilityValidator(), logger);
        var request = ProviderTestData.SelectionRequest(ProviderTestData.Catalog(ProviderTestData.Model("a")), ProviderTestData.Policy(candidates: ["a"]));

        _ = await selector.SelectAsync(request, TestContext.Current.CancellationToken);

        logger.Snapshot().ShouldContain(entry => entry.Message.Contains("Selected model", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SelectAsync_WhenNoCandidateMatches_LogsNoCompatibleModel()
    {
        var logger = new RecordingLogger<DefaultModelSelector>();
        var selector = new DefaultModelSelector(new DefaultModelCapabilityValidator(), logger);
        var request = ProviderTestData.SelectionRequest(ProviderTestData.Catalog(ProviderTestData.Model("other")), ProviderTestData.Policy(candidates: ["missing"]));

        _ = await selector.SelectAsync(request, TestContext.Current.CancellationToken);

        logger.Snapshot().ShouldContain(entry => entry.Message.Contains("No compatible model", StringComparison.Ordinal));
    }

    /// <summary>A validator that always throws, used to exercise the selector's generic-failure observability path.</summary>
    private sealed class ThrowingCapabilityValidator: IModelCapabilityValidator
    {
        public ValueTask<CapabilityValidationResult> ValidateAsync(
            ModelDescriptor model,
            ModelRequirements requirements,
            CapabilityDowngradePolicy downgradePolicy,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Validator exploded.");
    }

    [Fact]
    public async Task SelectAsync_WhenObserved_EmitsCorrelatedModelSelectionActivity()
    {
        Activity? stopped = null;
        using var parent = new Activity("test.model.select").Start();
        using var listener = CreateListener(AgentKitActivityNames.ModelSelect, parent, activity => stopped = activity);
        var selector = new DefaultModelSelector(new DefaultModelCapabilityValidator(), NullLogger<DefaultModelSelector>.Instance);
        var request = ProviderTestData.SelectionRequest(ProviderTestData.Catalog(ProviderTestData.Model("selected-model")), ProviderTestData.Policy(candidates: ["selected-model"]));
        _ = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.ModelSelect);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ModelRequestId).ShouldBe(request.ModelRequestId.ToString());
        activity.GetTagItem(AgentKitTagNames.RequestModel).ShouldBe("selected-model");
    }

    private static ActivityListener CreateListener(string operationName, Activity parent, Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == operationName && options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == operationName && activity.ParentSpanId == parent.SpanId)
                {
                    stopped(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
