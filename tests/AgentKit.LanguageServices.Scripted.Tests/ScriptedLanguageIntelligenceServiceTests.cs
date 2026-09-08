// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted.Tests;

public sealed class ScriptedLanguageIntelligenceServiceTests
{
    private static readonly LanguageQueryId _queryId = new(
        Guid.Parse("50000000-0000-0000-0000-000000000005"));

    [Fact]
    public void Constructor_WhenIntentIdsIsNull_ThrowsWithExactParameterName()
    {
        var options = Options.Create(new ScriptedLanguageOptions());

        var exception = Should.Throw<ArgumentNullException>(() => new ScriptedLanguageIntelligenceService(
            new TestGrantStore(),
            TimeProvider.System,
            options,
            NullLogger<ScriptedLanguageIntelligenceService>.Instance,
            null!));

        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task QueryAsync_WhenObserved_EmitsContentFreeQueryActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var service = Service(new TestGrantStore(), []);
        var request = Request();

        _ = await service.QueryAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.LanguageQuery);
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.LanguageQueryId).ShouldBe(request.Id.ToString());
        string.Join('|', activity.TagObjects.Select(static tag => $"{tag.Key}={tag.Value}"))
            .ShouldNotContain(request.Path.GetValueOrDefault().Value);
    }

    [Fact]
    public async Task QueryAsync_WhenScenarioMissing_ReturnsUnavailableWithoutConsumingGrant()
    {
        var store = new TestGrantStore();
        var service = Service(store, []);

        var result = await service.QueryAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(LanguageQueryStatus.Unavailable);
        store.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WhenResultKindMismatches_ReturnsFailedWithoutConsumingGrant()
    {
        var store = new TestGrantStore();
        var service = Service(store, [new ScriptedLanguageScenario(
            _queryId,
            Success(LanguageQueryKind.Hover),
            TimeSpan.Zero)]);

        var result = await service.QueryAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(LanguageQueryStatus.Failed);
        store.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WhenConfigured_ConsumesExactEvidenceAndReturnsDeclaredSnapshot()
    {
        var store = new TestGrantStore();
        var expected = Success(LanguageQueryKind.Diagnostics);
        var service = Service(store, [new ScriptedLanguageScenario(_queryId, expected, TimeSpan.Zero)]);
        var request = Request();

        var result = await service.QueryAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expected.Status);
        result.Kind.ShouldBe(expected.Kind);
        result.Complete.ShouldBe(expected.Complete);
        result.Locations.ShouldBeEmpty();
        result.Symbols.ShouldBeEmpty();
        result.Diagnostics.ShouldBeEmpty();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(service.SecurityAudience);
        enforcement.Kind.ShouldBe(SecurityOperationKind.FileRead);
        enforcement.Effect.ShouldBe(SecurityEffect.Observe);
        enforcement.Resources.ShouldBe([LanguageSecurityBinding.Resource(request.Kind, request.Path)]);
        enforcement.InputFingerprint.ShouldBe(LanguageSecurityBinding.Fingerprint(request));
    }

    [Fact]
    public async Task QueryAsync_WhenGrantConsumptionFails_ReturnsDeniedInsteadOfScenario()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Mismatch };
        var service = Service(store, [new ScriptedLanguageScenario(
            _queryId,
            Success(LanguageQueryKind.Diagnostics),
            TimeSpan.Zero)]);

        var result = await service.QueryAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(LanguageQueryStatus.Denied);
        result.SafeMessage.ShouldBe("Denied.");
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Reconciled, true, true)]
    [InlineData(GrantConsumptionStatus.Consumed, false, true)]
    [InlineData(GrantConsumptionStatus.Consumed, true, false)]
    public async Task QueryAsync_WhenReceiptDoesNotAuthorizeFreshIntent_DeniesBeforeScriptedResult(
        GrantConsumptionStatus status,
        bool includeReceipt,
        bool exactReceipt)
    {
        var store = new TestGrantStore
        {
            Status = status,
            IncludeReceipt = includeReceipt,
            ReturnExactReceipt = exactReceipt,
        };
        var service = Service(store, [new ScriptedLanguageScenario(
            _queryId,
            Success(LanguageQueryKind.Diagnostics),
            TimeSpan.Zero)]);

        var result = await service.QueryAsync(Request(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(LanguageQueryStatus.Denied);
        _ = store.Intents.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task QueryAsync_WhenCallerCancelsDuringNonCooperativeConsumption_PropagatesWithoutScriptedResult()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new TestGrantStore { OnConsume = cancellation.Cancel };
        var service = Service(store, [new ScriptedLanguageScenario(
            _queryId,
            Success(LanguageQueryKind.Diagnostics),
            TimeSpan.Zero)]);

        var action = async () => await service.QueryAsync(Request(), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task QueryAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grantStore = new InMemorySecurityGrantStore(time);
        var request = Request(TestGrantStore.CapturedGrant(
            _queryId,
            LanguageQueryKind.Diagnostics,
            new FileSystemPath("src/a.cs"),
            null,
            null,
            10,
            TimeSpan.FromSeconds(1)));
        await grantStore.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        var service = Service(
            grantStore,
            [new ScriptedLanguageScenario(_queryId, Success(LanguageQueryKind.Diagnostics), TimeSpan.Zero)],
            time);

        var result = await service.QueryAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(LanguageQueryStatus.Success);
    }

    [Fact]
    public async Task QueryAsync_WhenScenarioExceedsRequestedCount_TruncatesAndMarksIncomplete()
    {
        var location = new LanguageLocation(
            new FileSystemPath("src/a.cs"),
            new LanguageRange(new LanguagePosition(0, 0), new LanguagePosition(0, 1)),
            null);
        var oversized = new LanguageQueryResult(
            LanguageQueryStatus.Success,
            LanguageQueryKind.Diagnostics,
            null,
            [location, location],
            [],
            [
                new LanguageDiagnostic(LanguageDiagnosticSeverity.Error, "one", null, null, location),
                new LanguageDiagnostic(LanguageDiagnosticSeverity.Warning, "two", null, null, location),
            ],
            true,
            null);
        var service = Service(new TestGrantStore(), [new ScriptedLanguageScenario(_queryId, oversized, TimeSpan.Zero)]);

        var result = await service.QueryAsync(Request(maximumResults: 1), TestContext.Current.CancellationToken);

        result.Locations.Length.ShouldBe(1);
        result.Diagnostics.Length.ShouldBe(1);
        result.Complete.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAsync_WhenCancelledAfterAdmission_ReturnsTypedCancelledResult()
    {
        var time = new FakeTimeProvider();
        var store = new TestGrantStore();
        var service = Service(
            store,
            [new ScriptedLanguageScenario(_queryId, Success(LanguageQueryKind.Diagnostics), TimeSpan.FromMinutes(1))],
            time);
        using var cancellation = new CancellationTokenSource();

        var pending = service.QueryAsync(Request(), cancellation.Token).AsTask();
        _ = store.Enforcements.ShouldHaveSingleItem();
        await cancellation.CancelAsync();
        var result = await pending;

        result.Status.ShouldBe(LanguageQueryStatus.Cancelled);
    }

    [Fact]
    public void AddScriptedLanguageIntelligence_WhenCalled_RegistersReplaceableServiceAndIdentifierGenerator()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore, TestGrantStore>();

        _ = services.AddScriptedLanguageIntelligence(static _ => { });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ILanguageIntelligenceService>()
            .ShouldBeOfType<ScriptedLanguageIntelligenceService>();
        provider.GetRequiredService<IIdentifierGenerator<LanguageQueryId>>()
            .Create().Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddScriptedLanguageIntelligence_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("82000000-0000-0000-0000-000000000008"));
        var store = new TestGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
            new FixedSecurityEnforcementIntentIdGenerator(expectedId));
        _ = services.AddScriptedLanguageIntelligence(options => options.Scenarios.Add(new ScriptedLanguageScenario(
            _queryId,
            Success(LanguageQueryKind.Diagnostics),
            TimeSpan.Zero)));
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<ILanguageIntelligenceService>().QueryAsync(
            Request(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(LanguageQueryStatus.Success);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    private static ScriptedLanguageIntelligenceService Service(
        ISecurityGrantStore store,
        IEnumerable<ScriptedLanguageScenario> scenarios,
        TimeProvider? timeProvider = null)
    {
        var options = new ScriptedLanguageOptions();
        options.Scenarios.AddRange(scenarios);
        return new ScriptedLanguageIntelligenceService(
            store,
            timeProvider ?? TimeProvider.System,
            Options.Create(options));
    }

    private static LanguageQueryRequest Request(SecurityGrant? grant = null, int maximumResults = 10) => new(
        _queryId,
        LanguageQueryKind.Diagnostics,
        new FileSystemPath("src/a.cs"),
        null,
        null,
        maximumResults,
        TimeSpan.FromSeconds(1),
        grant ?? TestGrantStore.Grant());

    private static LanguageQueryResult Success(LanguageQueryKind kind) => new(
        LanguageQueryStatus.Success,
        kind,
        null,
        [],
        [],
        [],
        true,
        null);

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    private sealed class FixedSecurityEnforcementIntentIdGenerator(SecurityEnforcementIntentId value)
        : IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => value;
    }
}
