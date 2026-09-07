// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

public sealed class ObservabilityTests
{
    [Fact]
    public async Task ProcessAsync_WhenObserved_EmitsContentFreeTerminalActivity()
    {
        const string protectedContent = "never-export-output-content";
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var definition = TestFactory.Definition(OutputMode.Text);
        var processor = new DefaultOutputProcessor([], new AgentOutputOptionsSnapshot(1024, 8, 1, true, false));

        _ = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse(protectedContent)),
            TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.OutputValidate);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.OutputDefinitionId).ShouldBe(definition.Id.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedContent);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
