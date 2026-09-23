// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;



/// <summary>Verifies ScriptedProcessRunner behavior and contracts.</summary>
public sealed class ScriptedProcessRunnerTests
{
    private static readonly ProcessOperationId _operationId = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    [Fact]
    [Obsolete("Legacy IProcessRunner surface.")]
    public void Constructor_WhenLegacyLoggerArgumentIsNull_RetainsUnambiguousSourceCompatibility() => _ = new ScriptedProcessRunner(Resolver(), new TestGrantStore(), TimeProvider.System, Options.Create(new ScriptedProcessOptions()), null);
    [Fact]
    [Obsolete("Legacy IProcessRunner surface.")]
    public void Constructor_WhenIntentIdsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ScriptedProcessRunner(Resolver(), new TestGrantStore(), TimeProvider.System, Options.Create(new ScriptedProcessOptions()), null, null!));
        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    [Obsolete("Legacy IProcessRunner surface.")]
    public void Constructor_WhenScenarioOperationIdentitiesCollide_ThrowsArgumentException()
    {
        var options = new ScriptedProcessOptions();
        options.Scenarios.Add(new ScriptedProcessScenario(_operationId, Success("first"), TimeSpan.Zero));
        options.Scenarios.Add(new ScriptedProcessScenario(_operationId, Success("second"), TimeSpan.Zero));
        var exception = Should.Throw<ArgumentException>(() => new ScriptedProcessRunner(Resolver(), new TestGrantStore(), TimeProvider.System, Options.Create(options)));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Equality_WhenOperationIdResultAndDelayMatch_TreatsScenariosAsEqual()
    {
        var result = Success("done");
        var first = new ScriptedProcessScenario(_operationId, result, TimeSpan.Zero);
        var second = new ScriptedProcessScenario(_operationId, result, TimeSpan.Zero);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        (first with { }).ShouldBe(first);
    }

    [Fact]
    public async Task RunAsync_WhenResolverRevalidationChangesTheIntent_ReturnsResolutionFailedWithoutStartingTheScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var changedResolver = new FakeProcessIntentResolver(
            _ => new ProcessResolutionResult(ProcessResolutionStatus.ExecutableRejected, null, "changed before start"));
        var runner = Runner(changedResolver, new TestGrantStore(), Success("never"), TimeSpan.Zero, TimeProvider.System);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.ResolutionFailed);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
    }

    [Fact]
    [Obsolete("Legacy IProcessRunner surface.")]
    public async Task RunAsync_WhenNoScenarioIsConfiguredForTheOperation_ReturnsFailedWithoutConsumingTheGrant()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore();
        var options = new ScriptedProcessOptions();
        var runner = new ScriptedProcessRunner(resolver, store, TimeProvider.System, Options.Create(options));
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Failed);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        store.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenResolverThrowsUnexpectedException_PropagatesWithoutStartingTheScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var throwingResolver = new ThrowingProcessIntentResolver(new InvalidOperationException("boom"));
        var runner = Runner(throwingResolver, new TestGrantStore(), Success("never"), TimeSpan.Zero, TimeProvider.System);
        var action = async () => await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("boom");
    }

    [Fact]
    [Obsolete("Legacy IProcessRunner surface.")]
    public async Task RunAsync_WhenLoggerIsEnabled_EmitsCompletedStructuredEvent()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var options = new ScriptedProcessOptions();
        options.Scenarios.Add(new ScriptedProcessScenario(_operationId, Success("done"), TimeSpan.Zero));
        var logger = new RecordingLogger<ScriptedProcessRunner>();
        var runner = new ScriptedProcessRunner(resolver, new TestGrantStore(), TimeProvider.System, Options.Create(options), logger);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 12100);
    }

    [Fact]
    [Obsolete("Legacy IProcessRunner surface.")]
    public async Task RunAsync_WhenLoggerIsEnabledAndResolverThrows_EmitsFailedStructuredEvent()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var throwingResolver = new ThrowingProcessIntentResolver(new InvalidOperationException("boom"));
        var logger = new RecordingLogger<ScriptedProcessRunner>();
        var options = new ScriptedProcessOptions();
        options.Scenarios.Add(new ScriptedProcessScenario(_operationId, Success("never"), TimeSpan.Zero));
        var runner = new ScriptedProcessRunner(throwingResolver, new TestGrantStore(), TimeProvider.System, Options.Create(options), logger);
        var action = async () => await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 12101);
    }

    [Fact]
    public async Task RunAsync_WhenScenarioConfigured_ConsumesExactGrantAndReturnsDeclaredResult()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var expected = Success("done");
        var store = new TestGrantStore();
        var runner = Runner(resolver, store, expected, TimeSpan.Zero, TimeProvider.System);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.ShouldBe(expected);
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Resources.ShouldBe(ProcessSecurityBinding.Resources(intent));
        enforcement.InputFingerprint.ShouldBe(ProcessSecurityBinding.Fingerprint(intent));
    }

    [Fact]
    public async Task RunAsync_WhenGrantDenied_ReturnsNotStartedInsteadOfScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Unknown
        };
        var runner = Runner(resolver, store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
    }

    [Fact]
    public async Task RunAsync_WhenStoreReconcilesAnEarlierIntent_DoesNotStartTheScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Reconciled
        };
        var runner = Runner(resolver, store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        _ = store.Intents.ShouldHaveSingleItem();
        store.LegacyConsumptionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WhenConsumedResultLacksExactReceipt_DoesNotStartTheScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore
        {
            IncludeIntentReceipt = false
        };
        var runner = Runner(resolver, store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("enforcement-intent receipt");
    }

    [Fact]
    public async Task RunAsync_WhenReceiptDoesNotMatchFreshIntent_DoesNotStartTheScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore
        {
            ReturnExactIntentReceipt = false
        };
        var runner = Runner(resolver, store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
    }

    [Fact]
    public async Task RunAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new InMemorySecurityGrantStore(TimeProvider.System);
        var grant = CapturedGrantFactory.Create(new ComponentId("agentkit.processes.scripted"), ProcessSecurityBinding.Resources(intent), ProcessSecurityBinding.Fingerprint(intent));
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var runner = Runner(resolver, store, Success("captured"), TimeSpan.Zero, TimeProvider.System);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, grant), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.Completed);
    }

    [Fact]
    public async Task RunAsync_WhenCallerAlreadyCancelled_DoesNotConsumeOrStartTheScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore();
        var runner = Runner(resolver, store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = async () => await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        store.Intents.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCallerCancelsDuringNonCooperativeConsumption_DoesNotStartTheScenario()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        using var cancellation = new CancellationTokenSource();
        var store = new TestGrantStore
        {
            OnIntentConsumption = cancellation.Cancel
        };
        var runner = Runner(resolver, store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        var action = async () => await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenReceiptAccepted_UsesInjectedFreshIntentId()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("80000000-0000-0000-0000-000000000008"));
        var store = new TestGrantStore();
        var runner = Runner(resolver, store, Success("done"), TimeSpan.Zero, TimeProvider.System, new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterSimulatedStart_ReturnsUncertainCancellation()
    {
        var resolver = Resolver();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
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
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
        return new ScriptedProcessIntentResolver(Options.Create(options));
    }

    private static ScriptedProcessRunner Runner(IProcessIntentResolver resolver, ISecurityGrantStore store, ProcessRunResult result, TimeSpan delay, TimeProvider timeProvider, IIdentifierGenerator<SecurityEnforcementIntentId>? intentIds = null)
    {
        var options = new ScriptedProcessOptions();
        options.Scenarios.Add(new ScriptedProcessScenario(_operationId, result, delay));
        return intentIds is null ? new ScriptedProcessRunner(resolver, store, timeProvider, Options.Create(options)) : new ScriptedProcessRunner(resolver, store, timeProvider, Options.Create(options), null, intentIds);
    }

    private static ProcessResolveRequest Request() => new(_operationId, "tool", ["literal *", "$(never)"], new FileSystemPath("src"), [], [], new SandboxProfileId("scripted"), ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny, new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));
    private static ProcessRunResult Success(string text) => new(ProcessRunStatus.Exited, 0, [.. Encoding.UTF8.GetBytes(text)], [], Encoding.UTF8.GetByteCount(text), 0, false, false, ProcessSideEffectCertainty.Completed, null);
}
