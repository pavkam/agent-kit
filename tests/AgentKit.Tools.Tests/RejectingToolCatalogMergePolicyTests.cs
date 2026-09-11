// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.Conformance;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class RejectingToolCatalogMergePolicyTests: ToolCatalogMergePolicyConformanceTests, IAsyncLifetime
{
    private readonly List<ServiceProvider> _hosts = [];

    [Theory]
    [InlineData("selected")]
    [InlineData("rejected")]
    [InlineData("cancelled")]
    public async Task ResolveAsync_WhenObserved_UsesItsOwnCategoryAndSafeTerminalActivity(string outcome)
    {
        const string secret = "private-toolset-alias-content";
        var first = ToolCatalogMergeTestData.Candidate(alias: secret);
        var second = ToolCatalogMergeTestData.Candidate("second", "other", alias: secret);
        var request = ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]);
        var context = outcome == "rejected"
            ? new ToolCatalogMergeContext(request, [first, second], [new ToolCatalogIdentityCollision([first, second])])
            : new ToolCatalogMergeContext(request, [first], []);
        var logger = new RecordingLogger<RejectingToolCatalogMergePolicy>();
        using var parent = new Activity("observed-merge-policy").Start();
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) { observed = activity; } },
        };
        ActivitySource.AddActivityListener(listener);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled") { cancellation.Cancel(); }
        var policy = new RejectingToolCatalogMergePolicy(TimeProvider.System, logger);
        if (outcome == "cancelled") { _ = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.ResolveAsync(context, cancellation.Token)); }
        else { _ = await policy.ResolveAsync(context, cancellation.Token); }
        var activity = observed.ShouldNotBeNull();
        activity.ParentId.ShouldBe(parent.Id);
        activity.OperationName.ShouldBe(AgentKitActivityNames.ToolCatalogMergePolicy);
        activity.Status.ShouldBe(outcome == "selected" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        activity.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(secret, StringComparison.Ordinal));
        var logs = logger.Snapshot();
        logs.Select(static entry => entry.EventId.Id).ShouldBe([4060, 4061]);
        logs.ShouldAllBe(entry => entry.Category == typeof(RejectingToolCatalogMergePolicy).FullName);
        logs.ShouldAllBe(entry => !entry.Message.Contains(secret, StringComparison.Ordinal));
        logs[1].State["Outcome"].ShouldBe(outcome);
    }

    protected override IToolCatalogMergePolicy CreatePolicy()
    {
        var services = new ServiceCollection();
        _ = services.AddToolCatalogMerging();
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _hosts.Add(host);
        return host.GetRequiredService<IToolCatalogMergePolicy>();
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts) { await host.DisposeAsync(); }
    }

    [Fact]
    public async Task ResolveAsync_WhenInputsInvalid_RejectsBeforeObserving()
    {
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<RejectingToolCatalogMergePolicy>();
        Should.Throw<ArgumentNullException>(() => new RejectingToolCatalogMergePolicy(null!, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new RejectingToolCatalogMergePolicy(clock, null!)).ParamName.ShouldBe("logger");
        var policy = new RejectingToolCatalogMergePolicy(clock, logger);
        (await Should.ThrowAsync<ArgumentNullException>(async () => await policy.ResolveAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("context");
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveAsync_WhenCallerOmitsCollisionEvidence_StillRejectsDuplicateContributions(bool duplicateIdentity)
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("second", "source.other", duplicateIdentity ? "tool.read" : "tool.other", duplicateIdentity ? "other" : "read");
        var context = new ToolCatalogMergeContext(ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]), [first, second], []);
        _ = (await CreatePolicy().ResolveAsync(context, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolCatalogRejection>();
    }

    [Fact]
    public async Task ResolveAsync_WhenObserversThrow_StillTransfersSelection()
    {
        var logger = new RecordingLogger<RejectingToolCatalogMergePolicy> { ThrowOnWrite = true };
        var policy = new RejectingToolCatalogMergePolicy(new CallbackTimestampTimeProvider(static () => throw new InvalidOperationException("clock-content")), logger);
        _ = (await policy.ResolveAsync(new ToolCatalogMergeContext(ToolCaptureTestData.Discovery(), [], []), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolCatalogSelection>();
    }

    [Fact]
    public async Task ResolveAsync_WhenNoListenersEnabled_PreservesResult()
    {
        var policy = new RejectingToolCatalogMergePolicy(TimeProvider.System, NullLogger<RejectingToolCatalogMergePolicy>.Instance);
        _ = (await policy.ResolveAsync(new ToolCatalogMergeContext(ToolCaptureTestData.Discovery(), [], []), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolCatalogSelection>();
    }
}
