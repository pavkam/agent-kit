// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly DurableJournalKey JournalKey = new("json-journal");
    private static readonly DurableJournalKey OtherJournalKey = new("other-json-journal");

    [Fact]
    public void AddJsonDurableOperationJournal_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddJsonDurableOperationJournal(
                JournalKey, new JsonDurableTestRoot().Target()))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddJsonDurableOperationJournal_WhenTheTargetIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(
                () => new ServiceCollection().AddJsonDurableOperationJournal(JournalKey, null!))
            .ParamName.ShouldBe("target");

    [Fact]
    public void AddJsonDurableOperationJournal_WhenTheKeyIsDefault_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonDurableOperationJournal(
                default, new JsonDurableTestRoot().Target()))
            .ParamName.ShouldBe("key");

    /// <summary>Verifies an impossible bound fails at composition instead of during the first journaled operation.</summary>
    [Fact]
    public void AddJsonDurableOperationJournal_WhenAConfiguredBoundIsNotPositive_ThrowsAtRegistration() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddJsonDurableOperationJournal(
                JournalKey,
                new JsonDurableTestRoot().Target(),
                options => options.MaximumRecordBytes = 0))
            .ParamName.ShouldBe("maximumRecordBytes");

    [Fact]
    public void AddJsonDurableOperationJournal_WhenCalledTwiceWithTheSameKey_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();
        var target = new JsonDurableTestRoot().Target();

        _ = services
            .AddJsonDurableOperationJournal(JournalKey, target)
            .AddJsonDurableOperationJournal(JournalKey, target);

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableOperationJournal)
                && Equals(descriptor.ServiceKey, JournalKey.Value))
            .ShouldBe(1);
    }

    [Fact]
    public void AddJsonDurableOperationJournal_WhenCalledWithDistinctKeys_RegistersOnePerKey()
    {
        var services = new ServiceCollection();
        _ = AddSecurity(services);

        _ = services
            .AddJsonDurableOperationJournal(JournalKey, new JsonDurableTestRoot().Target())
            .AddJsonDurableOperationJournal(OtherJournalKey, new JsonDurableTestRoot().Target());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IDurableOperationJournal>(JournalKey.Value)
            .ShouldBeOfType<JsonDurableOperationJournal>().Key.ShouldBe(JournalKey);
        provider.GetRequiredKeyedService<IDurableOperationJournal>(OtherJournalKey.Value)
            .ShouldBeOfType<JsonDurableOperationJournal>().Key.ShouldBe(OtherJournalKey);
    }

    [Fact]
    public async Task AddJsonDurableOperationJournal_WhenResolvedByKey_RecordsAcceptance()
    {
        var services = new ServiceCollection();
        var harness = AddSecurity(services);
        _ = services.AddJsonDurableOperationJournal(JournalKey, new JsonDurableTestRoot().Target());
        using var provider = services.BuildServiceProvider();
        var journal = provider.GetRequiredKeyedService<IDurableOperationJournal>(JournalKey.Value)
            .ShouldBeOfType<JsonDurableOperationJournal>();
        await journal.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(JournalKey, new FencingToken(1));

        var result = await journal.RecordStartAsync(
            harness.Authorize(
                start,
                JournalKey,
                journal.SecurityAudience,
                start.Descriptor.Binding.Address,
                start.Descriptor.Binding.ExecutionContext.Authorization,
                DurableJournalSecurityBinding.Fingerprint(start),
                SecurityOperationKind.StateMutation,
                SecurityEffect.Create,
                new FencingToken(1)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
    }

    [Fact]
    public void AddJsonDurableOperationJournal_WhenClockIsAlreadyRegistered_PreservesIt()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        _ = services.AddSingleton<TimeProvider>(clock);

        _ = services.AddJsonDurableOperationJournal(JournalKey, new JsonDurableTestRoot().Target());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
    }

    /// <summary>Verifies a composition without an authority cannot produce a journal that skips enforcement.</summary>
    [Fact]
    public void AddJsonDurableOperationJournal_WhenSecurityIsMissing_FailsToResolveRatherThanSkippingEnforcement()
    {
        var services = new ServiceCollection();
        _ = services.AddJsonDurableOperationJournal(JournalKey, new JsonDurableTestRoot().Target());
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredKeyedService<IDurableOperationJournal>(JournalKey.Value));
    }

    /// <summary>Verifies this leaf registers no lease manager, because it cannot coordinate ownership across processes.</summary>
    [Fact]
    public void AddJsonDurableOperationJournal_WhenCalled_RegistersNoLeaseManager()
    {
        var services = new ServiceCollection();

        _ = services.AddJsonDurableOperationJournal(JournalKey, new JsonDurableTestRoot().Target());

        services.Any(descriptor => descriptor.ServiceType == typeof(IDurableLeaseManager)).ShouldBeFalse();
    }

    private static TestDurableSecurityHarness AddSecurity(IServiceCollection services)
    {
        var harness = new TestDurableSecurityHarness();
        _ = services.AddSingleton<ISecurityGrantStore>(harness);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(harness);
        return harness;
    }
}
