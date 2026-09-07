// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;

public sealed class ScriptedProcessTests
{
    private static readonly ProcessOperationId _operationId = new(
        Guid.Parse("50000000-0000-0000-0000-000000000005"));

    [Fact]
    public async Task ResolveAsync_WhenConfigured_ProducesDeterministicCanonicalEvidence()
    {
        var resolver = Resolver();

        var result = await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessResolutionStatus.Resolved);
        var intent = result.Intent.ShouldNotBeNull();
        intent.AbsoluteExecutablePath.ShouldBe("/scripted/bin/tool");
        intent.AbsoluteWorkingDirectory.ShouldBe("/scripted/workspace/src");
        intent.ExecutableFingerprint.ShouldBe(new ContentHash("sha256:tool"));
        intent.Request.Arguments.ShouldBe(["literal *", "$(never)"]);
    }

    [Fact]
    public async Task ResolveAsync_WhenEnvironmentIsNotAllowlisted_RejectsWithoutHostObservation()
    {
        var request = new ProcessResolveRequest(
            _operationId,
            "tool",
            [],
            null,
            [new ProcessEnvironmentVariable("PATH", "/bin")],
            [],
            new SandboxProfileId("scripted"),
            ProcessWorkspaceAccess.ReadOnly,
            ProcessSideEffectClass.ReadOnly,
            ProcessChildPolicy.Deny,
            new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));

        var result = await Resolver().ResolveAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessResolutionStatus.InvalidIntent);
    }

    [Fact]
    public async Task RunAsync_WhenScenarioConfigured_ConsumesExactGrantAndReturnsDeclaredResult()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken))
            .Intent.ShouldNotBeNull();
        var expected = Success("done");
        var store = new TestGrantStore();
        var runner = Runner(resolver, store, expected, TimeSpan.Zero, TimeProvider.System);

        var result = await runner.RunAsync(
            new ProcessRunRequest(intent, TestGrantStore.Grant()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(expected);
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Resources.ShouldBe(ProcessSecurityBinding.Resources(intent));
        enforcement.InputFingerprint.ShouldBe(ProcessSecurityBinding.Fingerprint(intent));
    }

    [Fact]
    public async Task RunAsync_WhenGrantDenied_ReturnsNotStartedInsteadOfScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken))
            .Intent.ShouldNotBeNull();
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Unknown };
        var runner = Runner(resolver, store, Success("never"), TimeSpan.Zero, TimeProvider.System);

        var result = await runner.RunAsync(
            new ProcessRunRequest(intent, TestGrantStore.Grant()),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterSimulatedStart_ReturnsUncertainCancellation()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken))
            .Intent.ShouldNotBeNull();
        var store = new TestGrantStore();
        var time = new FakeTimeProvider();
        var runner = Runner(resolver, store, Success("late"), TimeSpan.FromMinutes(1), time);
        using var cancellation = new CancellationTokenSource();

        var pending = runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), cancellation.Token).AsTask();
        _ = store.Enforcements.ShouldHaveSingleItem();
        await cancellation.CancelAsync();
        var result = await pending;

        result.Status.ShouldBe(ProcessRunStatus.Cancelled);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.MayHaveOccurred);
    }

    private static ScriptedProcessIntentResolver Resolver()
    {
        var options = new ScriptedProcessOptions();
        options.Executables.Add(new ScriptedExecutable(
            "tool",
            "/scripted/bin/tool",
            new ContentHash("sha256:tool")));
        return new ScriptedProcessIntentResolver(Options.Create(options));
    }

    private static ScriptedProcessRunner Runner(
        IProcessIntentResolver resolver,
        ISecurityGrantStore store,
        ProcessRunResult result,
        TimeSpan delay,
        TimeProvider timeProvider)
    {
        var options = new ScriptedProcessOptions();
        options.Scenarios.Add(new ScriptedProcessScenario(_operationId, result, delay));
        return new ScriptedProcessRunner(resolver, store, timeProvider, Options.Create(options));
    }

    private static ProcessResolveRequest Request() => new(
        _operationId,
        "tool",
        ["literal *", "$(never)"],
        new FileSystemPath("src"),
        [],
        [],
        new SandboxProfileId("scripted"),
        ProcessWorkspaceAccess.ReadOnly,
        ProcessSideEffectClass.ReadOnly,
        ProcessChildPolicy.Deny,
        new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));

    private static ProcessRunResult Success(string text) => new(
        ProcessRunStatus.Exited,
        0,
        [.. Encoding.UTF8.GetBytes(text)],
        [],
        Encoding.UTF8.GetByteCount(text),
        0,
        false,
        false,
        ProcessSideEffectCertainty.Completed,
        null);
}
