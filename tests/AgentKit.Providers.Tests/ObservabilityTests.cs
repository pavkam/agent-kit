// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>Verifies the content-free model catalog and selection diagnostics contract.</summary>
public sealed class ObservabilityTests
{
    [Fact]
    public async Task RefreshAsync_WhenObserved_EmitsPublishedCatalogActivity()
    {
        Activity? stopped = null;
        using var listener = CreateListener(activity => stopped = activity);
        var source = new StaticModelDescriptorSource(
            new ModelDescriptorSourceId("observed-source"),
            [ProviderTestData.Model("observed-model")]);
        using var catalog = new DefaultModelCatalog([source], NullLogger<DefaultModelCatalog>.Instance);

        var snapshot = await catalog.RefreshAsync(TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.ModelCatalogRefresh);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ModelCatalogVersion).ShouldBe(snapshot.Version.Value);
    }

    [Fact]
    public async Task SelectAsync_WhenObserved_EmitsCorrelatedModelSelectionActivity()
    {
        Activity? stopped = null;
        using var listener = CreateListener(activity => stopped = activity);
        var selector = new DefaultModelSelector(
            new DefaultModelCapabilityValidator(),
            NullLogger<DefaultModelSelector>.Instance);
        var request = ProviderTestData.SelectionRequest(
            ProviderTestData.Catalog(ProviderTestData.Model("selected-model")),
            ProviderTestData.Policy(candidates: ["selected-model"]));

        _ = await selector.SelectAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.ModelSelect);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ModelRequestId).ShouldBe(request.ModelRequestId.ToString());
        activity.GetTagItem(AgentKitTagNames.RequestModel).ShouldBe("selected-model");
    }

    private static ActivityListener CreateListener(Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = stopped,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
