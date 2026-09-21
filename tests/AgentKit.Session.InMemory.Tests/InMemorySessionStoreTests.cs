// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using System.Text.Json;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies InMemorySessionStore behavior and contracts.</summary>
public sealed class InMemorySessionStoreTests: SessionStoreConformanceTests<InMemorySessionStoreConformanceFixture>
{
    [Fact]
    public void Descriptor_WhenAccessed_ReportsNonDurable()
    {
        var store = TestFactory.CreateStore();
        store.Descriptor.Durable.ShouldBeFalse();
        store.Descriptor.Key.Value.ShouldBe("agentkit.in-memory");
    }

    [Fact]
    public void Constructor_WhenAuditRecordIdsIsNull_ThrowsArgumentNullException()
    {
        var security = new TestSecurityHarness();
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)), null!, security, security, TimeProvider.System));
        exception.ParamName.ShouldBe("auditRecordIds");
    }

    [Fact]
    public void Constructor_WhenBranchIdsIsNull_ThrowsArgumentNullException()
    {
        var security = new TestSecurityHarness();
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(null!, new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)), security, security, TimeProvider.System));
        exception.ParamName.ShouldBe("branchIds");
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var security = new TestSecurityHarness();
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)), new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)), security, security, null!));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var security = new TestSecurityHarness();
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(null!, new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)), new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)), security, security, TimeProvider.System));
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenOptionsValueIsNull_ThrowsArgumentNullException()
    {
        var security = new TestSecurityHarness();
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionStore(new NullValueOptions(), new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)), new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)), security, security, TimeProvider.System));
        exception.ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WhenMaximumIssuedReadSnapshotsIsNotPositive_ThrowsArgumentOutOfRangeException(int bound)
    {
        var security = new TestSecurityHarness();
        var options = Options.Create(new InMemorySessionStoreOptions { MaximumIssuedReadSnapshots = bound });
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new InMemorySessionStore(options, new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)), new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)), security, security, TimeProvider.System));
        exception.ParamName.ShouldBe("options");
        exception.ActualValue.ShouldBe(bound);
    }

    [Fact]
    public void Constructor_WhenMaximumIssuedReadSnapshotsIsOne_Constructs()
    {
        var store = TestFactory.CreateStore(options: new InMemorySessionStoreOptions { MaximumIssuedReadSnapshots = 1 });
        store.Descriptor.Key.Value.ShouldBe("agentkit.in-memory");
    }

    [Fact]
    public async Task ReadAsync_WhenIssuedSnapshotsExceedBoundOfOne_EvictsOldestSnapshotAndReportsItUnavailable()
    {
        var (store, descriptor, context) = await SeedAsync(3, new InMemorySessionStoreOptions { MaximumIssuedReadSnapshots = 1 });
        var firstPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2), TestContext.Current.CancellationToken);
        var appendedEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 4, "later");
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(3), new IdempotencyKey("later"), [appendedEntry]), TestContext.Current.CancellationToken);
        var secondPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2), TestContext.Current.CancellationToken);

        var continued = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 10, firstPage.Snapshot!), TestContext.Current.CancellationToken);
        var latest = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, secondPage.ThroughSequence, 10, secondPage.Snapshot!), TestContext.Current.CancellationToken);

        secondPage.Snapshot.ShouldNotBe(firstPage.Snapshot);
        continued.ShouldBeOfType<SessionReadFailed>().SafeMessage.ShouldBe("The supplied session read snapshot is not available for this branch.");
        latest.ShouldBeOfType<SessionPage>().Snapshot.ShouldBe(secondPage.Snapshot);
    }

    [Fact]
    public async Task ReadAsync_WhenIssuedSnapshotsFitBoundOfTwo_RetainsBothSnapshots()
    {
        var (store, descriptor, context) = await SeedAsync(3, new InMemorySessionStoreOptions { MaximumIssuedReadSnapshots = 2 });
        var firstPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2), TestContext.Current.CancellationToken);
        var appendedEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 4, "later");
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(3), new IdempotencyKey("later"), [appendedEntry]), TestContext.Current.CancellationToken);
        var secondPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2), TestContext.Current.CancellationToken);

        var continued = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 10, firstPage.Snapshot!), TestContext.Current.CancellationToken);
        var latest = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, secondPage.ThroughSequence, 10, secondPage.Snapshot!), TestContext.Current.CancellationToken);

        secondPage.Snapshot.ShouldNotBe(firstPage.Snapshot);
        var continuedPage = continued.ShouldBeOfType<SessionPage>();
        continuedPage.Snapshot.ShouldBe(firstPage.Snapshot);
        continuedPage.Entries.ShouldHaveSingleItem().Sequence.ShouldBe(new SessionSequence(3));
        latest.ShouldBeOfType<SessionPage>().Snapshot.ShouldBe(secondPage.Snapshot);
    }

    /// <summary>An options wrapper whose <see cref="IOptions{TOptions}.Value"/> is null, which the store must reject.</summary>
    private sealed class NullValueOptions: IOptions<InMemorySessionStoreOptions>
    {
        public InMemorySessionStoreOptions Value => null!;
    }

    [Fact]
    public async Task Operations_WhenContextTenantDiffers_DoNotDiscloseOrMutateSession()
    {
        var store = TestFactory.CreateStore();
        var created = await store.CreateAsync(TestFactory.CreateRequest(), TestContext.Current.CancellationToken);
        var descriptor = ((SessionCreated) created).Descriptor;
        var foreign = TestFactory.OperationContext(descriptor.Address, tenant: "tenant-2");
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        _ = (await store.LoadAsync(foreign, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionNotFound>();
        _ = (await store.AppendAsync(new SessionAppendRequest(foreign, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("foreign-append"), [entry]), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendNotFound>();
        _ = (await store.ReadAsync(new SessionReadRequest(foreign, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionReadNotFound>();
        _ = (await store.CreateBranchAsync(new SessionBranchRequest(foreign, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("foreign-branch")), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionBranchParentNotFound>();
        _ = (await store.DeleteAsync(new SessionDeleteRequest(foreign, new IdempotencyKey("foreign-delete")), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionDeleted>();
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(TestFactory.OperationContext(descriptor.Address), descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenIdenticalReconstructedRequestReplays_ReturnsOriginalReceipt()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("create-replay");
        var first = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        var replay = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        replay.ShouldBe(first);
        replay.Existing.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedEvidence_RejectsBeforeVersionConflict()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var key = new IdempotencyKey("append-replay");
        var first = new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), key, [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "one")]);
        _ = await store.AppendAsync(first, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(TestFactory.OperationContext(descriptor.Address, principal: "user-2"), descriptor.ActiveBranchId, new SessionVersion(99), key, [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 100, "two")]);
        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedPayload_Rejects()
    {
        var (store, descriptor, request) = await CreateAppendRequestAsync();
        _ = await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(request.Context, request.BranchId, request.ExpectedVersion, request.IdempotencyKey, [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "changed")]);
        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedPrincipal_Rejects()
    {
        var (store, descriptor, request) = await CreateAppendRequestAsync();
        _ = await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(TestFactory.OperationContext(descriptor.Address, principal: "other"), request.BranchId, request.ExpectedVersion, request.IdempotencyKey, request.Entries);
        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedCorrelation_Rejects()
    {
        var (store, descriptor, request) = await CreateAppendRequestAsync();
        _ = await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(TestFactory.OperationContext(descriptor.Address), request.BranchId, request.ExpectedVersion, request.IdempotencyKey, request.Entries);
        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenEquivalentReconstructedPayloadReplays_ReturnsOriginalReceiptBeforeVersionConflict()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("test.payload", new ExtensionValue([.. /*lang=json,strict*/"{\"nested\":[1,2]}"u8])));
        var message = new UserMessage(new MessageId(Guid.NewGuid()), descriptor.Address.AgentId, descriptor.Address.SessionId, null, descriptor.ActiveBranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("payload", TextSemantics.Plain, extensions)], extensions);
        var entry = new MessageSessionEntry(new SessionEntryId(Guid.NewGuid()), descriptor.Address, TestFactory.Correlation(), descriptor.ActiveBranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), message);
        var key = new IdempotencyKey("reconstructed");
        var first = (SessionAppended) await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), key, [entry]), TestContext.Current.CancellationToken);
        var rebuiltExtensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("test.payload", new ExtensionValue([.. /*lang=json,strict*/"{\"nested\":[1,2]}"u8])));
        var rebuiltMessage = new UserMessage(message.Id, descriptor.Address.AgentId, descriptor.Address.SessionId, null, descriptor.ActiveBranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("payload", TextSemantics.Plain, rebuiltExtensions)], rebuiltExtensions);
        var rebuiltEntry = new MessageSessionEntry(entry.Id, descriptor.Address, entry.Correlation, descriptor.ActiveBranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), rebuiltMessage);
        var replay = (SessionAppended) await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), key, [rebuiltEntry]), TestContext.Current.CancellationToken);
        replay.ShouldBe(first);
    }

    [Fact]
    public async Task CreateAsync_WhenSameKeyIsUsedByDifferentTenants_CreatesIndependentSessions()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("tenant-scoped");
        var first = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        var second = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key, identity: TestFactory.Identity("tenant-2")), TestContext.Current.CancellationToken);
        second.Descriptor.Address.ShouldNotBe(first.Descriptor.Address);
    }

    [Fact]
    public async Task CreateAsync_WhenOriginalCreateWasDeleted_DoesNotResurrectAndNewKeyCreatesFreshSession()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("original");
        var request = TestFactory.CreateRequest(agentId, key);
        var original = (SessionCreated) await store.CreateAsync(request, TestContext.Current.CancellationToken);
        _ = await store.DeleteAsync(new SessionDeleteRequest(TestFactory.OperationContext(original.Descriptor.Address), new IdempotencyKey("delete")), TestContext.Current.CancellationToken);
        _ = (await store.CreateAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreateFailed>();
        var recreated = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, new IdempotencyKey("fresh")), TestContext.Current.CancellationToken);
        recreated.Descriptor.Address.ShouldNotBe(original.Descriptor.Address);
    }

    [Fact]
    public async Task DeleteAsync_WhenEquivalentRequestReplays_ReturnsOriginalReceipt()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionDeleteRequest(context, new IdempotencyKey("delete-replay"));
        var first = (SessionDeleted) await store.DeleteAsync(request, TestContext.Current.CancellationToken);
        var replay = (SessionDeleted) await store.DeleteAsync(request, TestContext.Current.CancellationToken);
        replay.ShouldBe(first);
    }

    [Fact]
    public async Task DeleteAsync_WhenIdempotencyKeyHasChangedPrincipal_Rejects()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var key = new IdempotencyKey("delete-principal");
        _ = await store.DeleteAsync(new SessionDeleteRequest(TestFactory.OperationContext(descriptor.Address), key), TestContext.Current.CancellationToken);
        _ = (await store.DeleteAsync(new SessionDeleteRequest(TestFactory.OperationContext(descriptor.Address, principal: "other"), key), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionDeleteFailed>();
    }

    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionAppendRequest Request)> CreateAppendRequestAsync()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("independent"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "original")]);
        return (store, descriptor, request);
    }

    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionOperationContext Context)> SeedAsync(int entryCount, InMemorySessionStoreOptions? options = null)
    {
        var store = TestFactory.CreateStore(options: options);
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        for (var i = 0; i < entryCount; i++)
        {
            var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, i + 1, $"entry-{i}");
            _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(i), new IdempotencyKey($"seed-{i}"), [entry]), TestContext.Current.CancellationToken);
        }

        return (store, descriptor, context);
    }

    [Fact]
    public async Task ReadAsync_FromBeginning_ReturnsAllEntriesWhenPageSizeIsLargeEnough()
    {
        var (store, descriptor, context) = await SeedAsync(3);
        var result = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        var page = result.ShouldBeOfType<SessionPage>();
        page.Entries.Length.ShouldBe(3);
        page.HasMore.ShouldBeFalse();
        page.ThroughSequence.Value.ShouldBe(3);
    }

    private static readonly DateTimeOffset _now = new(2026, 9, 8, 14, 0, 0, TimeSpan.Zero);
    [Theory]
    [InlineData("intent")]
    [InlineData("grant")]
    [InlineData("request")]
    [InlineData("fence")]
    [InlineData("fingerprint")]
    [InlineData("enforcement")]
    public async Task CreateAsync_WhenGrantStoreReturnsWrongReceipt_RejectsBeforeAuditOrMutation(string corruptedField)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants)
        {
            IntentResultInterceptor = (result, _, _) => result.IntentReceipt is { } receipt ? new GrantConsumptionResult(result.Status, result.RemainingUses, result.SafeMessage, Corrupt(receipt, corruptedField)) : result,
        };
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        using var activities = CreateActivityCollector(lower.Address.AgentId);
        var rejected = await store.CreateAsync(wrapper, TestContext.Current.CancellationToken);
        _ = rejected.ShouldBeOfType<SessionCreateFailed>();
        audit.Calls.ShouldBe(0);
        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("rejected");
        grants.IntentResultInterceptor = null;
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WhenActualGrantStoreReturnsReconciledReceipt_DoesNotRepeatAccess()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        var created = await store.CreateAsync(wrapper, TestContext.Current.CancellationToken);
        var replay = await store.CreateAsync(wrapper, TestContext.Current.CancellationToken);
        _ = created.ShouldBeOfType<SessionCreated>();
        _ = replay.ShouldBeOfType<SessionCreateFailed>();
        audit.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_WhenGrantStoreCancelsThenReturnsDenialOrWrongReceipt_PropagatesCancellationBeforeAccess(bool wrongReceipt)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        using var cancellation = new CancellationTokenSource();
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants)
        {
            IntentResultInterceptor = (result, _, _) =>
            {
                cancellation.Cancel();
                if (!wrongReceipt)
                {
                    return new GrantConsumptionResult(GrantConsumptionStatus.Mismatch, result.RemainingUses, "The enforcement evidence differs.");
                }

                var receipt = result.IntentReceipt.ShouldNotBeNull();
                return new GrantConsumptionResult(result.Status, result.RemainingUses, result.SafeMessage, Corrupt(receipt, "intent"));
            },
        };
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await store.CreateAsync(wrapper, cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(0);
        grants.IntentResultInterceptor = null;
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WhenGrantStoreCancelsThenReturnsReconciledReceipt_PropagatesCancellationWithoutRepeatAccess()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants);
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(wrapper, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>();
        using var cancellation = new CancellationTokenSource();
        grants.IntentResultInterceptor = (result, _, _) =>
        {
            result.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
            cancellation.Cancel();
            return result;
        };
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await store.CreateAsync(wrapper, cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_WhenAuditCancelsThenReturnsNonSuccess_PropagatesCancellationBeforeAccess(bool failed)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        using var cancellation = new CancellationTokenSource();
        var audit = new RecordingSecurityAuditDispatcher
        {
            BeforeAccepted = cancellation.Cancel,
            NextResult = failed ? new SecurityAuditFailed("The required audit sink failed.") : new SecurityAuditUnavailable("The required audit sink is unavailable."),
        };
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await store.CreateAsync(wrapper, cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(1);
        audit.BeforeAccepted = null;
        audit.NextResult = new SecurityAuditAccepted();
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_WhenCallerCancelsAfterConsumptionOrAudit_DoesNotAccessState(bool cancelAfterAudit)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants);
        var audit = new RecordingSecurityAuditDispatcher();
        using var cancellation = new CancellationTokenSource();
        if (cancelAfterAudit)
        {
            audit.BeforeAccepted = cancellation.Cancel;
        }
        else
        {
            grants.IntentResultInterceptor = (result, _, _) =>
            {
                if (result.Status == GrantConsumptionStatus.Consumed)
                {
                    cancellation.Cancel();
                }

                return result;
            };
        }

        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await store.CreateAsync(wrapper, cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(cancelAfterAudit ? 1 : 0);
        grants.IntentResultInterceptor = null;
        audit.BeforeAccepted = null;
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(cancelAfterAudit ? 2 : 1);
    }

    private static InMemorySessionStore CreateStore(ISecurityGrantStore grants, ISecurityAuditDispatcher audit, TimeProvider timeProvider, ILogger<InMemorySessionStore>? logger = null)
    {
        Debug.Assert(grants is not null, "A grant store is required.");
        Debug.Assert(audit is not null, "An audit dispatcher is required.");
        Debug.Assert(timeProvider is not null, "A deterministic clock is required.");
        return new InMemorySessionStore(new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)), new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)), audit, grants, timeProvider, logger);
    }

    private static async ValueTask<AuthorizedSessionStoreRequest<SessionStoreCreateRequest>> AuthorizeCreateAsync(ISecurityGrantStore grants, InMemorySessionStore store, SessionStoreCreateRequest request)
    {
        Debug.Assert(grants is not null, "A grant store is required.");
        Debug.Assert(store is not null, "A session store is required.");
        Debug.Assert(request is not null, "A lower session creation request is required.");
        var context = request.Context;
        var grant = new SecurityGrant(new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), context.Authorization.Scope, context.Identity, context.Authorization, store.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Create, [SessionStoreSecurityBinding.Resource(store.Descriptor.Key, request.Address)], SessionStoreSecurityBinding.Fingerprint(request), context.Authorization.PolicySnapshot.Version, new SecurityRevocationVersion(1), _now.AddMinutes(-1), _now.AddMinutes(5), 1);
        await grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
        return new AuthorizedSessionStoreRequest<SessionStoreCreateRequest>(request, store.Descriptor.Key, grant, new SecurityEnforcementIntent(new SecurityEnforcementIntentId(Guid.NewGuid()), null));
    }

    private static SecurityEnforcementIntentReceipt Corrupt(SecurityEnforcementIntentReceipt receipt, string field)
    {
        Debug.Assert(receipt is not null, "A fresh grant-store receipt is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(field), "A selected corruption axis is required.");
        var enforcement = field == "enforcement" ? new SecurityEnforcementRequest(receipt.Enforcement.Scope, receipt.Enforcement.Identity, receipt.Enforcement.Authorization!, receipt.Enforcement.Audience, receipt.Enforcement.Kind, receipt.Enforcement.Effect, receipt.Enforcement.Resources, new InputFingerprint("sha256:wrong-enforcement"), receipt.Enforcement.RevocationVersion) : receipt.Enforcement;
        return new SecurityEnforcementIntentReceipt(field == "intent" ? new SecurityEnforcementIntentId(Guid.NewGuid()) : receipt.IntentId, field == "grant" ? new GrantId(Guid.NewGuid()) : receipt.GrantId, field == "request" ? new SecurityRequestId(Guid.NewGuid()) : receipt.RequestId, enforcement, field == "fence" ? new FencingToken(1) : receipt.RequiredFence, field == "fingerprint" ? new ContentHash("sha256:wrong-effect") : receipt.EffectFingerprint, receipt.ConsumedAt);
    }

    private static ActivityCollector CreateActivityCollector(AgentId agentId)
    {
        Debug.Assert(agentId != default, "A non-default agent identity is required.");
        return new ActivityCollector(static source => source.Name == AgentKitDiagnostics.ActivitySourceName, activity => activity.OperationName == AgentKitActivityNames.SessionStoreOperation && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(agentId.ToString()) == true && activity.GetTagItem(AgentKitTagNames.SessionOperation)?.Equals("create") == true);
    }

    [Fact]
    public async Task AppendAsync_WhenContentChangesUnderIssuedGrant_DeniesBeforeSessionAccess()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var originalEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "allowed");
        var original = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("content-bound"), [originalEntry]);
        var security = TestSecurityHarness.For(store);
        var authorized = security.Authorize(store, original, SecurityOperationKind.StateMutation, SecurityEffect.Append);
        var changedEntry = originalEntry with
        {
            Message = originalEntry.Message with
            {
                Parts = [new TextPart("changed", TextSemantics.Plain, ExtensionData.Empty)],
            },
        };
        var changed = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, original.IdempotencyKey, [changedEntry]);
        var tampered = new AuthorizedSessionStoreRequest<SessionAppendRequest>(changed, authorized.StoreKey, authorized.Grant, authorized.Intent);
        var result = await store.AppendAsync(tampered, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionAppendFailed>();
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AppendAsync_WhenJsonPayloadChangesUnderIssuedGrant_DeniesBeforeSessionAccess(bool toolArguments)
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var originalEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        var callId = new ToolCallId(Guid.NewGuid());
        var originalPart = JsonPart(toolArguments, callId, "allowed");
        originalEntry = originalEntry with
        {
            Message = originalEntry.Message with
            {
                Parts = [originalPart]
            }
        };
        var original = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("json-bound"), [originalEntry]);
        var security = TestSecurityHarness.For(store);
        var authorized = security.Authorize(store, original, SecurityOperationKind.StateMutation, SecurityEffect.Append);
        var changedEntry = originalEntry with
        {
            Message = originalEntry.Message with
            {
                Parts = [JsonPart(toolArguments, callId, "changed")]
            },
        };
        var changed = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, original.IdempotencyKey, [changedEntry]);
        var tampered = new AuthorizedSessionStoreRequest<SessionAppendRequest>(changed, authorized.StoreKey, authorized.Grant, authorized.Intent);
        var result = await store.AppendAsync(tampered, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionAppendFailed>();
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenExpectedVersionMatches_AdvancesVersionAndCommitsEntries()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        var result = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("a1"), [entry]), TestContext.Current.CancellationToken);
        var appended = result.ShouldBeOfType<SessionAppended>();
        appended.NewVersion.Value.ShouldBe(1);
        _ = appended.CommittedEntries.ShouldHaveSingleItem();
        appended.CommittedEntries[0].Id.ShouldBe(entry.Id);
    }

    private static ContentPart JsonPart(bool toolArguments, ToolCallId callId, string target)
    {
        using var document = JsonDocument.Parse($$"""{"target":"{{target}}"}""");
        var value = document.RootElement.Clone();
        return toolArguments ? new ToolCallPart(callId, new ToolReference(new ToolAlias("test"), null, null), value, null, ExtensionData.Empty) : new StructuredDataPart(value, null, ExtensionData.Empty);
    }

    [Fact]
    public async Task AppendAsync_WhenExpectedVersionIsStale_ReturnsConflictAndDoesNotMutate()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("a1"), [entry]), TestContext.Current.CancellationToken);
        // Retry against the now-stale version 0.
        var staleEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "stale");
        var result = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("a2"), [staleEntry]), TestContext.Current.CancellationToken);
        var conflict = result.ShouldBeOfType<SessionAppendConflict>();
        conflict.ExpectedVersion.Value.ShouldBe(0);
        conflict.ActualVersion.Value.ShouldBe(1);
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.Length.ShouldBe(1);
    }

    [Fact]
    public async Task AppendAsync_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);
        var entry = TestFactory.MessageEntry(address, new BranchId(Guid.NewGuid()), 1);
        var result = await store.AppendAsync(new SessionAppendRequest(context, new BranchId(Guid.NewGuid()), new SessionVersion(0), new IdempotencyKey("x"), [entry]), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionAppendNotFound>();
    }

    [Fact]
    public async Task AppendAsync_WhenBranchDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var unknownBranch = new BranchId(Guid.NewGuid());
        var entry = TestFactory.MessageEntry(descriptor.Address, unknownBranch, 1);
        var result = await store.AppendAsync(new SessionAppendRequest(context, unknownBranch, new SessionVersion(0), new IdempotencyKey("x"), [entry]), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionAppendNotFound>();
    }

    [Fact]
    public async Task AppendAsync_WhenEntrySequenceDoesNotMatchExpectedVersion_ReturnsFailed()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var wronglySequencedEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 5);
        var result = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("x"), [wronglySequencedEntry]), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task CreateAsync_WhenCalledOnce_ReturnsNewActiveSession()
    {
        var store = TestFactory.CreateStore();
        var result = await store.CreateAsync(TestFactory.CreateRequest(), TestContext.Current.CancellationToken);
        var created = result.ShouldBeOfType<SessionCreated>();
        created.Existing.ShouldBeFalse();
        created.Descriptor.State.ShouldBe(SessionLifecycleState.Active);
        created.Descriptor.Version.Value.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_WhenCalledTwiceWithSameIdempotencyKey_ReturnsSameSession()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("retry-key");
        var first = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        var second = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        second.Existing.ShouldBeFalse();
        second.Descriptor.Address.ShouldBe(first.Descriptor.Address);
    }

    [Fact]
    public async Task CreateAsync_WhenCalledWithDifferentIdempotencyKeys_ReturnsDifferentSessions()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var first = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId), TestContext.Current.CancellationToken);
        var second = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId), TestContext.Current.CancellationToken);
        second.Descriptor.Address.ShouldNotBe(first.Descriptor.Address);
    }

    [Fact]
    public async Task CreateAsync_WhenCalled_RecordsConfiguredStoreKey()
    {
        var store = TestFactory.CreateStore();
        var created = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(), TestContext.Current.CancellationToken);
        created.Descriptor.StoreKey.ShouldBe(store.Descriptor.Key);
    }

    /// <inheritdoc/>
    protected override InMemorySessionStoreConformanceFixture CreateFixture() => new();
    [Fact]
    public async Task LoadAsync_WhenSessionExists_ReturnsCurrentDescriptor()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var result = await store.LoadAsync(TestFactory.OperationContext(descriptor.Address), TestContext.Current.CancellationToken);
        var loaded = result.ShouldBeOfType<SessionLoaded>();
        loaded.Descriptor.Address.ShouldBe(descriptor.Address);
    }

    [Fact]
    public async Task LoadAsync_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var result = await store.LoadAsync(TestFactory.OperationContext(address), TestContext.Current.CancellationToken);
        var notFound = result.ShouldBeOfType<SessionNotFound>();
        notFound.Address.ShouldBe(address);
    }

    [Fact]
    public async Task LoadAsync_AfterDelete_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        _ = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("del-1")), TestContext.Current.CancellationToken);
        var result = await store.LoadAsync(context, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionNotFound>();
    }

    [Fact]
    public async Task AdmitInputAsync_WhenAdmissionIdentityCollides_DoesNotAdvanceAnySessionState()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var identity = TestFactory.Identity();
        var context = TestFactory.LaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), identity);
        var profile = new SessionProfileReference(new SessionProfileKey("default"), new SessionProfileVersion(1));
        var configuration = new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(new SessionExecutionLaneProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), profile, configuration, DateTimeOffset.UnixEpoch, new IdempotencyKey("provision-collision")), TestContext.Current.CancellationToken);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var first = Admission(context, admissionId, new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "first");
        var firstAccepted = (AcceptedInput) await store.AdmitInputAsync(first, TestContext.Current.CancellationToken);
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, first.EntryId);
        var collision = Admission(context, admissionId, new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), currentVersion, currentRevision, currentCursor, "collision");
        var subsequent = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), currentVersion, currentRevision, currentCursor, "subsequent");
        var rejected = await store.AdmitInputAsync(collision, TestContext.Current.CancellationToken);
        var accepted = await store.AdmitInputAsync(subsequent, TestContext.Current.CancellationToken);
        _ = rejected.ShouldBeOfType<InputConflict>();
        firstAccepted.Receipt.AdmittedSequence.Value.ShouldBe(2);
        accepted.ShouldBeOfType<AcceptedInput>().Receipt.AdmittedSequence.Value.ShouldBe(3);
    }

    [Fact]
    public async Task AcceptRunAsync_WhenLaneAndAdmissionAreCurrent_CommitsRecoverableStateAndReplaysBeforeVersionChecks()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var identity = TestFactory.Identity();
        var operationId = new OperationId(Guid.NewGuid());
        var beforeCorrelation = new BeforeRunOperationCorrelation(operationId, null);
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(descriptor.Address, laneId, beforeCorrelation, identity);
        var profile = new SessionProfileReference(new SessionProfileKey("default"), new SessionProfileVersion(1));
        var configuration = new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provision = new SessionExecutionLaneProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), profile, configuration, DateTimeOffset.UnixEpoch, new IdempotencyKey("provision"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(provision, TestContext.Current.CancellationToken);
        var input = new AgentInput(new InputId(Guid.NewGuid()), InputDelivery.FollowUp, [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var admission = new SessionInputAdmissionRequest(context, admissionId, new SessionEntryId(Guid.NewGuid()), input, input, new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint("sha256:original"), new InputFingerprint("sha256:effective")), DateTimeOffset.UnixEpoch.AddSeconds(1), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, new IdempotencyKey("admit"), 8);
        var admitted = (AcceptedInput) await store.AdmitInputAsync(admission, TestContext.Current.CancellationToken);
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var inRunCorrelation = new InRunOperationCorrelation(operationId, runId, turnId);
        var inRunAuthorization = TestFactory.Authorization(descriptor.Address.AgentId, descriptor.Address.SessionId, inRunCorrelation, identity);
        var start = new SessionRunStartRequest(context, admissionId, [admissionId], admitted.Receipt.AdmittedSequence, new SessionLaneRevision(provisioned.LaneRevision.Value + 1), new SessionVersion(provisioned.SessionVersion.Value + 1), new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId), null, runId, turnId, new SessionEntryId(Guid.NewGuid()), [new SessionEntryId(Guid.NewGuid())], [new MessageId(Guid.NewGuid())], new SessionEntryId(Guid.NewGuid()), new OperationStateRevision(1), profile, configuration, inRunAuthorization, DateTimeOffset.UnixEpoch.AddSeconds(2), new IdempotencyKey("start"));
        var colliding = new SessionRunStartRequest(context, admissionId, [admissionId], admitted.Receipt.AdmittedSequence, start.ExpectedLaneRevision, start.ExpectedVersion, start.BranchCursor, null, runId, turnId, provision.EntryId, start.EntryIds, start.MessageIds, start.AcceptedEntryId, start.OperationStateRevision, profile, configuration, inRunAuthorization, start.AcceptedAt, new IdempotencyKey("colliding-start"));
        var collision = await store.AcceptRunAsync(colliding, TestContext.Current.CancellationToken);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);
        var replay = (SessionRunAccepted) await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);
        var loadContext = TestFactory.LaneContext(descriptor.Address, laneId, inRunCorrelation, identity);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(new SessionRunStateRequest(loadContext), TestContext.Current.CancellationToken);
        _ = collision.ShouldBeOfType<SessionRunStartConflict>();
        accepted.Existing.ShouldBeFalse();
        replay.Existing.ShouldBeTrue();
        replay.State.ShouldBe(accepted.State);
        loaded.State.ShouldBe(accepted.State);
        accepted.State.State.ShouldBe(DurableOperationState.Accepted);
        accepted.State.InitiatingAdmissionId.ShouldBe(admissionId);
        accepted.State.PromotedAdmissionIds.ShouldBe([admissionId]);
        accepted.State.CommittedCursor.LastEntryId.ShouldBe(start.AcceptedEntryId);
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(loadContext, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.OfType<InputPromotedSessionEntry>().Single().InitiatingAdmissionId.ShouldBe(admissionId);
        page.Entries[^1].ShouldBeOfType<OperationAcceptedSessionEntry>().State.InitiatingAdmissionId.ShouldBe(admissionId);
    }

    [Fact]
    public async Task AcceptRunAsync_WhenInitiatingCorrelationDiffers_RejectsWithoutMutationAndRetainsTriggerForRecovery()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var identity = TestFactory.Identity();
        var storedOperationId = new OperationId(Guid.NewGuid());
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var storedContext = TestFactory.LaneContext(descriptor.Address, laneId, new BeforeRunOperationCorrelation(storedOperationId, null), identity);
        var profile = new SessionProfileReference(new SessionProfileKey("default"), new SessionProfileVersion(1));
        var configuration = new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(new SessionExecutionLaneProvisionRequest(storedContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), profile, configuration, DateTimeOffset.UnixEpoch, new IdempotencyKey("provision-correlation")), TestContext.Current.CancellationToken);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var admission = Admission(storedContext, admissionId, new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit-correlation");
        var admitted = (AcceptedInput) await store.AdmitInputAsync(admission, TestContext.Current.CancellationToken);
        var expectedVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var expectedLaneRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var expectedCursor = new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId);
        var substitutedContext = TestFactory.LaneContext(descriptor.Address, laneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), identity);
        var rejectedStart = Start(substitutedContext, admissionId, admitted.Receipt.AdmittedSequence, expectedLaneRevision, expectedVersion, expectedCursor, profile, configuration, "reject-correlation");
        var rejected = await store.AcceptRunAsync(rejectedStart, TestContext.Current.CancellationToken);
        var afterRejection = (SessionLoaded) await store.LoadAsync(storedContext, TestContext.Current.CancellationToken);
        var rejectedPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(storedContext, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<SessionRunStartConflict>().Kind.ShouldBe(SessionRunStartConflictKind.AdmissionCorrelation);
        afterRejection.Descriptor.Version.ShouldBe(expectedVersion);
        rejectedPage.Entries.Length.ShouldBe(2);
        rejectedPage.Entries.ShouldNotContain(static entry => entry is InputPromotedSessionEntry);
        rejectedPage.Entries.ShouldNotContain(static entry => entry is OperationAcceptedSessionEntry);
        var acceptedStart = Start(storedContext, admissionId, admitted.Receipt.AdmittedSequence, expectedLaneRevision, expectedVersion, expectedCursor, profile, configuration, "accept-correlation");
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(acceptedStart, TestContext.Current.CancellationToken);
        var inRunContext = TestFactory.LaneContext(descriptor.Address, laneId, new InRunOperationCorrelation(storedOperationId, acceptedStart.RunId, acceptedStart.InitialTurnId), identity);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(new SessionRunStateRequest(inRunContext), TestContext.Current.CancellationToken);
        var acceptedPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(inRunContext, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        loaded.State.ShouldBe(accepted.State);
        loaded.State.Correlation.OperationId.ShouldBe(storedOperationId);
        loaded.State.InitiatingAdmissionId.ShouldBe(admissionId);
        acceptedPage.Entries.OfType<InputPromotedSessionEntry>().Single().InitiatingAdmissionId.ShouldBe(admissionId);
        acceptedPage.Entries[^1].ShouldBeOfType<OperationAcceptedSessionEntry>().State.InitiatingAdmissionId.ShouldBe(admissionId);
    }

    private static SessionRunStartRequest Start(SessionOperationContext context, AdmissionId admissionId, SessionSequence cutoff, SessionLaneRevision expectedLaneRevision, SessionVersion expectedVersion, SessionBranchCursor branchCursor, SessionProfileReference profile, RunConfigurationReference configuration, string idempotencyKey)
    {
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var inRunCorrelation = new InRunOperationCorrelation(context.Correlation.OperationId, runId, turnId);
        return new SessionRunStartRequest(context, admissionId, [admissionId], cutoff, expectedLaneRevision, expectedVersion, branchCursor, null, runId, turnId, new SessionEntryId(Guid.NewGuid()), [new SessionEntryId(Guid.NewGuid())], [new MessageId(Guid.NewGuid())], new SessionEntryId(Guid.NewGuid()), new OperationStateRevision(1), profile, configuration, TestFactory.Authorization(context.AgentId, context.SessionId, inRunCorrelation, context.Identity), DateTimeOffset.UnixEpoch.AddSeconds(2), new IdempotencyKey(idempotencyKey));
    }

    private static SessionInputAdmissionRequest Admission(SessionOperationContext context, AdmissionId admissionId, InputId inputId, SessionEntryId entryId, SessionVersion version, SessionLaneRevision laneRevision, SessionBranchCursor cursor, string key)
    {
        var input = new AgentInput(inputId, InputDelivery.FollowUp, [new TextPart(key, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new SessionInputAdmissionRequest(context, admissionId, entryId, input, input, new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint($"sha256:{key}:original"), new InputFingerprint($"sha256:{key}:effective")), DateTimeOffset.UnixEpoch.AddSeconds(1), version, laneRevision, cursor, new IdempotencyKey(key), 8);
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionExists_RemovesIt()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var result = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("d1")), TestContext.Current.CancellationToken);
        var deleted = result.ShouldBeOfType<SessionDeleted>();
        deleted.Address.ShouldBe(descriptor.Address);
        var loadResult = await store.LoadAsync(context, TestContext.Current.CancellationToken);
        _ = loadResult.ShouldBeOfType<SessionNotFound>();
    }

    [Fact]
    public async Task DeleteAsync_WhenCalledTwice_IsIdempotent()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        _ = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("d1")), TestContext.Current.CancellationToken);
        var second = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("d2")), TestContext.Current.CancellationToken);
        _ = second.ShouldBeOfType<SessionDeleted>();
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionNeverExisted_StillReportsDeleted()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);
        var result = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("d1")), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionDeleted>();
    }

    [Fact]
    public async Task CreateAsync_WhenObserved_EmitsCorrelatedStoreActivity()
    {
        var store = TestFactory.CreateStore();
        var request = TestFactory.CreateRequest();
        using var activities = CreateStoreOperationCollector(request.AgentId, "create");
        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AgentKitActivityNames.SessionStoreOperation);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionOperation).ShouldBe("create");
    }

    private static ActivityCollector CreateStoreOperationCollector(AgentId agentId, string operation) => new(static source => source.Name == AgentKitDiagnostics.ActivitySourceName, activity => activity.OperationName == AgentKitActivityNames.SessionStoreOperation && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(agentId.ToString()) == true && activity.GetTagItem(AgentKitTagNames.SessionOperation)?.Equals(operation) == true);

    // ---- CreateAsync: address collision without a matching idempotency key. ----

    [Fact]
    public async Task CreateAsync_WhenAllocatedAddressAlreadyPresentWithDifferentIdempotencyKey_ReturnsFailed()
    {
        var store = TestFactory.CreateStore();
        var identity = TestFactory.Identity();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var security = TestSecurityHarness.For(store);
        var first = ManualCreateRequest(address, identity, new IdempotencyKey("collision-first"));
        _ = await store.CreateAsync(security.Authorize(store, first, SecurityOperationKind.StateMutation, SecurityEffect.Create), TestContext.Current.CancellationToken);
        var second = ManualCreateRequest(address, identity, new IdempotencyKey("collision-second"));

        var result = await store.CreateAsync(security.Authorize(store, second, SecurityOperationKind.StateMutation, SecurityEffect.Create), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("The allocated session address is already present.");
    }

    private static SessionStoreCreateRequest ManualCreateRequest(SessionAddress address, ExecutionIdentity identity, IdempotencyKey key)
    {
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var logical = new SessionCreateRequest(address.AgentId, identity, TestFactory.Authorization(address.AgentId, null, correlation, identity), null, key, ExtensionData.Empty);
        var context = new SessionOperationContext(address.AgentId, address.SessionId, null, correlation, identity,
            TestFactory.Authorization(address.AgentId, address.SessionId, correlation, identity));
        return new SessionStoreCreateRequest(logical, address, context);
    }

    // ---- AppendAsync: entry and message identity reservation across separate append calls. ----

    [Fact]
    public async Task AppendAsync_WhenEntryIdentityAlreadyReserved_ReturnsFailed()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "first");
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("entry-reserved-1"), [entry]), TestContext.Current.CancellationToken);
        var reused = entry with
        {
            Sequence = new SessionSequence(2),
            Message = entry.Message with { Id = new MessageId(Guid.NewGuid()) },
        };

        var result = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(1), new IdempotencyKey("entry-reserved-2"), [reused]), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldBe("An appended entry identity is already reserved.");
    }

    [Fact]
    public async Task AppendAsync_WhenMessageIdentityAlreadyReserved_ReturnsFailed()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "first");
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("message-reserved-1"), [entry]), TestContext.Current.CancellationToken);
        var reused = entry with
        {
            Id = new SessionEntryId(Guid.NewGuid()),
            Sequence = new SessionSequence(2),
        };

        var result = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(1), new IdempotencyKey("message-reserved-2"), [reused]), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldBe("An appended message identity is already reserved.");
    }

    // ---- ProvisionLaneAsync: idempotency, conflict, and stale-evidence rejections. ----

    private static SessionProfileReference DefaultProfile() => new(new SessionProfileKey("default"), new SessionProfileVersion(1));

    private static RunConfigurationReference DefaultConfiguration() =>
        new(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));

    private static SessionExecutionLaneProvisionRequest ProvisionRequest(
        SessionOperationContext context, SessionBranchCursor cursor, SessionVersion version, SessionEntryId entryId, string key) =>
        new(context, cursor, version, entryId, DefaultProfile(), DefaultConfiguration(), DateTimeOffset.UnixEpoch, new IdempotencyKey(key));

    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionOperationContext Context)> SeedLaneContextAsync()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var identity = TestFactory.Identity();
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(descriptor.Address, laneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), identity);
        return (store, descriptor, context);
    }

    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionOperationContext Context, SessionExecutionLaneProvisioned Provisioned)> ProvisionLaneAsync(string key = "provision")
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), key),
            TestContext.Current.CancellationToken);
        return (store, descriptor, context, provisioned);
    }

    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionOperationContext Context, SessionExecutionLaneProvisioned Provisioned, SessionInputAdmissionRequest Admission, AcceptedInput Accepted)> PrepareLaneAsync(string key = "prepare")
    {
        var (store, descriptor, context, provisioned) = await ProvisionLaneAsync(key);
        var admission = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, key);
        var accepted = (AcceptedInput) await store.AdmitInputAsync(admission, TestContext.Current.CancellationToken);
        return (store, descriptor, context, provisioned, admission, accepted);
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenSessionDoesNotExist_ReturnsRejected()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(address, laneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), TestFactory.Identity());
        var request = ProvisionRequest(context, new SessionBranchCursor(new BranchId(Guid.NewGuid()), null), new SessionVersion(0), new SessionEntryId(Guid.NewGuid()), "unavailable");

        var result = await store.ProvisionLaneAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>().SafeMessage.ShouldBe("The session is unavailable.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenRetriedWithSameKeyAndEvidence_ReturnsExistingAsExisting()
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        var request = ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), "replay");

        var first = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(request, TestContext.Current.CancellationToken);
        var second = await store.ProvisionLaneAsync(request, TestContext.Current.CancellationToken);

        var replay = second.ShouldBeOfType<SessionExecutionLaneProvisioned>();
        replay.Existing.ShouldBeTrue();
        replay.ExecutionLaneId.ShouldBe(first.ExecutionLaneId);
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenRetriedWithSameKeyAndDifferentEvidence_ReturnsConflict()
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        var entryId = new SessionEntryId(Guid.NewGuid());
        var request = ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, entryId, "mismatch");
        _ = await store.ProvisionLaneAsync(request, TestContext.Current.CancellationToken);
        var changed = ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), new SessionVersion(descriptor.Version.Value + 5), entryId, "mismatch");

        var result = await store.ProvisionLaneAsync(changed, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldBe("The provisioning idempotency key was reused with different evidence.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenLaneAlreadyProvisioned_ReturnsConflict()
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        _ = await store.ProvisionLaneAsync(ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), "first"), TestContext.Current.CancellationToken);

        var result = await store.ProvisionLaneAsync(ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), "second"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldBe("The execution lane is already provisioned.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenExpectedVersionIsStale_ReturnsConflict()
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        _ = await store.ProvisionLaneAsync(ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), "seed"), TestContext.Current.CancellationToken);
        var otherLaneId = new ExecutionLaneId(Guid.NewGuid());
        var otherContext = TestFactory.LaneContext(descriptor.Address, otherLaneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), context.Identity);

        var result = await store.ProvisionLaneAsync(ProvisionRequest(otherContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), "stale-version"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldBe("The expected session version is stale.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenBranchCursorIsStale_ReturnsConflict()
    {
        var (store, descriptor, context, provisioned) = await ProvisionLaneAsync("cursor-seed");
        var otherLaneId = new ExecutionLaneId(Guid.NewGuid());
        var otherContext = TestFactory.LaneContext(descriptor.Address, otherLaneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), context.Identity);
        var staleCursor = new SessionBranchCursor(descriptor.ActiveBranchId, null);

        var result = await store.ProvisionLaneAsync(ProvisionRequest(otherContext, staleCursor, provisioned.SessionVersion, new SessionEntryId(Guid.NewGuid()), "stale-cursor"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldBe("The branch cursor is stale or unavailable.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenBranchAlreadyOwnedByAnotherLane_ReturnsConflict()
    {
        var (store, descriptor, context, first) = await ProvisionLaneAsync("owner-seed");
        var otherLaneId = new ExecutionLaneId(Guid.NewGuid());
        var otherContext = TestFactory.LaneContext(descriptor.Address, otherLaneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), context.Identity);

        var result = await store.ProvisionLaneAsync(ProvisionRequest(otherContext, first.BranchCursor, first.SessionVersion, new SessionEntryId(Guid.NewGuid()), "owner-second"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldBe("The branch is already owned by another execution lane.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenEntryIdAlreadyReserved_ReturnsConflict()
    {
        var (store, descriptor, context, first) = await ProvisionLaneAsync("entry-seed");
        var branchResult = (SessionBranched) await store.CreateBranchAsync(new SessionBranchRequest(TestFactory.OperationContext(descriptor.Address), descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("entry-seed-fork")), TestContext.Current.CancellationToken);
        var otherLaneId = new ExecutionLaneId(Guid.NewGuid());
        var otherContext = TestFactory.LaneContext(descriptor.Address, otherLaneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), context.Identity);
        var expectedVersion = new SessionVersion(first.SessionVersion.Value + 1);
        var reusedEntryId = new SessionEntryId(first.BranchCursor.LastEntryId!.Value.Value);

        var result = await store.ProvisionLaneAsync(ProvisionRequest(otherContext, new SessionBranchCursor(branchResult.NewBranchId, null), expectedVersion, reusedEntryId, "entry-second"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldBe("The provisioning entry identity is already reserved.");
    }

    // ---- AdmitInputAsync: idempotency, conflict, capacity, and staleness rejections. ----

    private static SessionInputAdmissionRequest AdmissionWithLimit(
        SessionOperationContext context, AdmissionId admissionId, InputId inputId, SessionEntryId entryId,
        SessionVersion version, SessionLaneRevision laneRevision, SessionBranchCursor cursor, string key, int maximumPendingInputs)
    {
        var input = new AgentInput(inputId, InputDelivery.FollowUp, [new TextPart(key, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new SessionInputAdmissionRequest(context, admissionId, entryId, input, input,
            new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint($"sha256:{key}:original"), new InputFingerprint($"sha256:{key}:effective")),
            DateTimeOffset.UnixEpoch.AddSeconds(1), version, laneRevision, cursor, new IdempotencyKey(key), maximumPendingInputs);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenSessionDoesNotExist_ReturnsRejected()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(address, laneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), TestFactory.Identity());
        var admission = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionVersion(0), new SessionLaneRevision(1), new SessionBranchCursor(new BranchId(Guid.NewGuid()), null), "unavailable");

        var result = await store.AdmitInputAsync(admission, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.SafeReason.ShouldBe("The session is unavailable.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenRetriedWithSameIdempotencyKeyAndEvidence_ReturnsExistingReceipt()
    {
        var (store, _, context, provisioned) = await ProvisionLaneAsync("admit-replay");
        var request = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit-replay");

        var first = (AcceptedInput) await store.AdmitInputAsync(request, TestContext.Current.CancellationToken);
        var second = await store.AdmitInputAsync(request, TestContext.Current.CancellationToken);

        var replay = second.ShouldBeOfType<AcceptedInput>();
        replay.Receipt.Existing.ShouldBeTrue();
        replay.Receipt.AdmissionId.ShouldBe(first.Receipt.AdmissionId);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenRetriedWithSameIdempotencyKeyAndDifferentEvidence_ReturnsConflict()
    {
        var (store, _, context, provisioned) = await ProvisionLaneAsync("admit-mismatch");
        var entryId = new SessionEntryId(Guid.NewGuid());
        var first = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), entryId, provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit-mismatch");
        _ = await store.AdmitInputAsync(first, TestContext.Current.CancellationToken);
        var changed = new SessionInputAdmissionRequest(context, new AdmissionId(Guid.NewGuid()), entryId, first.OriginalPayload, first.EffectivePayload, first.Preprocessing, first.AdmittedAt, provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, first.IdempotencyKey, first.MaximumPendingInputs);

        var result = await store.AdmitInputAsync(changed, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason.ShouldBe("The admission idempotency key was reused with different evidence.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenSameInputAdmittedWithNewIdempotencyKeyAndMatchingEvidence_ReturnsExistingReceipt()
    {
        var (store, descriptor, context, provisioned) = await ProvisionLaneAsync("admit-input-replay");
        var inputId = new InputId(Guid.NewGuid());
        var first = Admission(context, new AdmissionId(Guid.NewGuid()), inputId, new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit-input-replay");
        var firstAccepted = (AcceptedInput) await store.AdmitInputAsync(first, TestContext.Current.CancellationToken);
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, first.EntryId);
        var replay = new SessionInputAdmissionRequest(context, new AdmissionId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), first.OriginalPayload, first.EffectivePayload, first.Preprocessing, first.AdmittedAt, currentVersion, currentRevision, currentCursor, new IdempotencyKey("admit-input-replay-new-key"), first.MaximumPendingInputs);

        var result = await store.AdmitInputAsync(replay, TestContext.Current.CancellationToken);

        var accepted = result.ShouldBeOfType<AcceptedInput>();
        accepted.Receipt.Existing.ShouldBeTrue();
        accepted.Receipt.AdmissionId.ShouldBe(firstAccepted.Receipt.AdmissionId);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenSameInputAdmittedWithNewIdempotencyKeyAndDifferentEvidence_ReturnsConflict()
    {
        var (store, descriptor, context, provisioned) = await ProvisionLaneAsync("admit-input-conflict");
        var inputId = new InputId(Guid.NewGuid());
        var first = Admission(context, new AdmissionId(Guid.NewGuid()), inputId, new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit-input-conflict");
        _ = await store.AdmitInputAsync(first, TestContext.Current.CancellationToken);
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, first.EntryId);
        var differentPayload = new AgentInput(inputId, InputDelivery.FollowUp, [new TextPart("changed", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var conflicting = new SessionInputAdmissionRequest(context, new AdmissionId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), differentPayload, differentPayload, first.Preprocessing, first.AdmittedAt, currentVersion, currentRevision, currentCursor, new IdempotencyKey("admit-input-conflict-new-key"), first.MaximumPendingInputs);

        var result = await store.AdmitInputAsync(conflicting, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason.ShouldBe("The input identity was already admitted with different immutable evidence.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenExpectedVersionIsStale_ReturnsRejected()
    {
        var (store, _, context, provisioned) = await ProvisionLaneAsync("admit-stale-version");
        var stale = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionVersion(provisioned.SessionVersion.Value + 99), provisioned.LaneRevision, provisioned.BranchCursor, "admit-stale-version");

        var result = await store.AdmitInputAsync(stale, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.SafeReason.ShouldBe("The expected session version is stale.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenLaneNotProvisioned_ReturnsRejected()
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        var admission = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), descriptor.Version, new SessionLaneRevision(1), new SessionBranchCursor(descriptor.ActiveBranchId, null), "no-lane");

        var result = await store.AdmitInputAsync(admission, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.SafeReason.ShouldBe("The execution lane is not provisioned.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenLaneRevisionIsStale_ReturnsRejected()
    {
        var (store, _, context, provisioned) = await ProvisionLaneAsync("admit-stale-revision");
        var stale = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, new SessionLaneRevision(provisioned.LaneRevision.Value + 5), provisioned.BranchCursor, "admit-stale-revision");

        var result = await store.AdmitInputAsync(stale, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.SafeReason.ShouldBe("The expected lane revision or branch cursor is stale.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenEntryIdAlreadyReserved_ReturnsConflict()
    {
        var (store, descriptor, context, provisioned) = await ProvisionLaneAsync("admit-entry-reserved");
        var reusedEntryId = new SessionEntryId(Guid.NewGuid());
        var first = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), reusedEntryId, provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit-entry-reserved-first");
        _ = await store.AdmitInputAsync(first, TestContext.Current.CancellationToken);
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, first.EntryId);
        var second = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), reusedEntryId, currentVersion, currentRevision, currentCursor, "admit-entry-reserved-second");

        var result = await store.AdmitInputAsync(second, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason.ShouldBe("The admission entry identity is already reserved.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenPendingQueueIsFull_ReturnsQueueCapacityExceeded()
    {
        var (store, descriptor, context, provisioned) = await ProvisionLaneAsync("admit-capacity");
        var first = AdmissionWithLimit(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit-capacity-first", maximumPendingInputs: 1);
        _ = await store.AdmitInputAsync(first, TestContext.Current.CancellationToken);
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, first.EntryId);
        var second = AdmissionWithLimit(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), currentVersion, currentRevision, currentCursor, "admit-capacity-second", maximumPendingInputs: 1);

        var result = await store.AdmitInputAsync(second, TestContext.Current.CancellationToken);

        var exceeded = result.ShouldBeOfType<QueueCapacityExceeded>();
        exceeded.Limit.MaximumPendingInputs.ShouldBe(1);
        exceeded.Limit.CurrentPendingInputs.ShouldBe(1);
    }

    // ---- AcceptRunAsync: rejections and conflicts beyond the primary success/replay path. ----

    private static SessionRunStartRequest RunStartRequest(
        SessionOperationContext context,
        AdmissionId initiatingAdmissionId,
        ImmutableArray<AdmissionId> selectedAdmissionIds,
        SessionSequence promotionCutoff,
        SessionLaneRevision expectedLaneRevision,
        SessionVersion expectedVersion,
        SessionBranchCursor branchCursor,
        string idempotencyKey,
        FencingToken? expectedFencingToken = null,
        ImmutableArray<MessageId>? messageIds = null)
    {
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var inRunCorrelation = new InRunOperationCorrelation(context.Correlation.OperationId, runId, turnId);
        var authorization = TestFactory.Authorization(context.AgentId, context.SessionId, inRunCorrelation, context.Identity);
        return new SessionRunStartRequest(context, initiatingAdmissionId, selectedAdmissionIds, promotionCutoff,
            expectedLaneRevision, expectedVersion, branchCursor, expectedFencingToken, runId, turnId,
            new SessionEntryId(Guid.NewGuid()), [new SessionEntryId(Guid.NewGuid())],
            messageIds ?? [new MessageId(Guid.NewGuid())], new SessionEntryId(Guid.NewGuid()),
            new OperationStateRevision(1), DefaultProfile(), DefaultConfiguration(), authorization,
            DateTimeOffset.UnixEpoch.AddSeconds(2), new IdempotencyKey(idempotencyKey));
    }

    [Fact]
    public async Task AcceptRunAsync_WhenSessionDoesNotExist_ReturnsRejected()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(address, laneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), TestFactory.Identity());
        var admissionId = new AdmissionId(Guid.NewGuid());
        var start = RunStartRequest(context, admissionId, [admissionId], new SessionSequence(1), new SessionLaneRevision(1), new SessionVersion(0), new SessionBranchCursor(new BranchId(Guid.NewGuid()), null), "accept-unavailable");

        var result = await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartRejected>().SafeReason.ShouldBe("The session is unavailable.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenLaneDoesNotExist_ReturnsConflict()
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        var admissionId = new AdmissionId(Guid.NewGuid());
        var start = RunStartRequest(context, admissionId, [admissionId], new SessionSequence(1), new SessionLaneRevision(1), descriptor.Version, new SessionBranchCursor(descriptor.ActiveBranchId, null), "accept-no-lane");

        var result = await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.LaneRevision);
        conflict.SafeReason.ShouldBe("The selected lane does not exist.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenLaneRevisionIsStale_ReturnsConflict()
    {
        var (store, _, context, provisioned) = await ProvisionLaneAsync("accept-stale-revision");
        var admissionId = new AdmissionId(Guid.NewGuid());
        var start = RunStartRequest(context, admissionId, [admissionId], new SessionSequence(1), new SessionLaneRevision(provisioned.LaneRevision.Value + 5), provisioned.SessionVersion, provisioned.BranchCursor, "accept-stale-revision-start");

        var result = await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.LaneRevision);
        conflict.SafeReason.ShouldBe("The selected lane revision is stale.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenBranchCursorIsStale_ReturnsConflict()
    {
        var (store, descriptor, context, provisioned) = await ProvisionLaneAsync("accept-stale-cursor");
        var admissionId = new AdmissionId(Guid.NewGuid());
        var staleCursor = new SessionBranchCursor(descriptor.ActiveBranchId, null);
        var start = RunStartRequest(context, admissionId, [admissionId], new SessionSequence(1), provisioned.LaneRevision, provisioned.SessionVersion, staleCursor, "accept-stale-cursor-start");

        var result = await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.BranchCursor);
        conflict.SafeReason.ShouldBe("The selected branch cursor is stale.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenSessionVersionIsStale_ReturnsConflict()
    {
        var (store, _, context, provisioned) = await ProvisionLaneAsync("accept-stale-version");
        var admissionId = new AdmissionId(Guid.NewGuid());
        var start = RunStartRequest(context, admissionId, [admissionId], new SessionSequence(1), provisioned.LaneRevision, new SessionVersion(provisioned.SessionVersion.Value + 99), provisioned.BranchCursor, "accept-stale-version-start");

        var result = await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.SessionVersion);
        conflict.SafeReason.ShouldBe("The expected session version is stale.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenSelectedAdmissionBelongsToDifferentIdentity_ReturnsConflict()
    {
        var (store, descriptor, context, provisioned, admission, accepted) = await PrepareLaneAsync("accept-identity-mismatch");
        var otherIdentity = TestExecutionIdentity.Create(context.Identity.TenantId, new PrincipalId("someone-else"), ExecutionSubjectKind.Human);
        var otherContext = new SessionOperationContext(context.AgentId, context.SessionId, context.ExecutionLaneId, context.Correlation, otherIdentity,
            TestFactory.Authorization(context.AgentId, context.SessionId, context.Correlation, otherIdentity));
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId);
        var start = RunStartRequest(otherContext, admission.AdmissionId, [admission.AdmissionId], accepted.Receipt.AdmittedSequence, new SessionLaneRevision(provisioned.LaneRevision.Value + 1), new SessionVersion(provisioned.SessionVersion.Value + 1), currentCursor, "accept-identity-mismatch-start");

        var result = await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.AdmissionIdentity);
        conflict.SafeReason.ShouldBe("Every promoted admission must retain the exact authorized run identity.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenPromotionCutoffExcludesSelectedAdmission_ReturnsConflict()
    {
        var (store, descriptor, context, provisioned, admission, accepted) = await PrepareLaneAsync("accept-cutoff-exclude");
        var lowCutoff = new SessionSequence(accepted.Receipt.AdmittedSequence.Value - 1);
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId);
        var start = RunStartRequest(context, admission.AdmissionId, [admission.AdmissionId], lowCutoff, new SessionLaneRevision(provisioned.LaneRevision.Value + 1), new SessionVersion(provisioned.SessionVersion.Value + 1), currentCursor, "accept-cutoff-exclude-start");

        var result = await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.PromotionPlan);
        conflict.SafeReason.ShouldBe("The exact promotion plan is no longer eligible.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenMessageIdAlreadyReserved_ReturnsConflict()
    {
        var (store, descriptor, context, provisioned, admission, accepted) = await PrepareLaneAsync("accept-message-reuse");
        var firstCursor = new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId);
        var firstStart = RunStartRequest(context, admission.AdmissionId, [admission.AdmissionId], accepted.Receipt.AdmittedSequence, new SessionLaneRevision(provisioned.LaneRevision.Value + 1), new SessionVersion(provisioned.SessionVersion.Value + 1), firstCursor, "accept-message-reuse-first");
        var firstAccepted = (SessionRunAccepted) await store.AcceptRunAsync(firstStart, TestContext.Current.CancellationToken);
        var reusedMessageId = firstStart.MessageIds[0];

        var branch = (SessionBranched) await store.CreateBranchAsync(new SessionBranchRequest(TestFactory.OperationContext(descriptor.Address), descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("accept-message-reuse-fork")), TestContext.Current.CancellationToken);
        var secondLaneId = new ExecutionLaneId(Guid.NewGuid());
        var secondContext = TestFactory.LaneContext(descriptor.Address, secondLaneId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), context.Identity);
        var secondProvision = new SessionExecutionLaneProvisionRequest(secondContext, new SessionBranchCursor(branch.NewBranchId, null), new SessionVersion(firstAccepted.SessionVersion.Value + 1), new SessionEntryId(Guid.NewGuid()), DefaultProfile(), DefaultConfiguration(), DateTimeOffset.UnixEpoch, new IdempotencyKey("accept-message-reuse-second-provision"));
        var secondProvisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(secondProvision, TestContext.Current.CancellationToken);
        var secondAdmission = Admission(secondContext, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), secondProvisioned.SessionVersion, secondProvisioned.LaneRevision, secondProvisioned.BranchCursor, "accept-message-reuse-second-admit");
        var secondAccepted = (AcceptedInput) await store.AdmitInputAsync(secondAdmission, TestContext.Current.CancellationToken);
        var secondStart = RunStartRequest(secondContext, secondAdmission.AdmissionId, [secondAdmission.AdmissionId], secondAccepted.Receipt.AdmittedSequence, new SessionLaneRevision(secondProvisioned.LaneRevision.Value + 1), new SessionVersion(secondProvisioned.SessionVersion.Value + 1), new SessionBranchCursor(branch.NewBranchId, secondAdmission.EntryId), "accept-message-reuse-second-start", messageIds: [reusedMessageId]);

        var result = await store.AcceptRunAsync(secondStart, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.PromotionPlan);
        conflict.SafeReason.ShouldBe("A reserved message identity is already in use.");
    }

    // ---- ReleaseRunAsync: session- and lane-absence rejections. ----

    [Fact]
    public async Task ReleaseRunAsync_WhenSessionDoesNotExist_ReturnsRejected()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(address, laneId, new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null), TestFactory.Identity());
        var release = new SessionRunReleaseRequest(context, new OperationStateRevision(1), new SessionVersion(0), new IdempotencyKey("release-missing"));

        var result = await store.ReleaseRunAsync(release, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<SessionRunReleaseRejected>();
        rejected.Kind.ShouldBe(SessionRunReleaseRejectionKind.LaneNotFound);
        rejected.SafeReason.ShouldBe("The session is unavailable.");
    }

    [Fact]
    public async Task ReleaseRunAsync_WhenLaneDoesNotExist_ReturnsRejected()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(descriptor.Address, laneId, new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null), TestFactory.Identity());
        var release = new SessionRunReleaseRequest(context, new OperationStateRevision(1), descriptor.Version, new IdempotencyKey("release-no-lane"));

        var result = await store.ReleaseRunAsync(release, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<SessionRunReleaseRejected>();
        rejected.Kind.ShouldBe(SessionRunReleaseRejectionKind.LaneNotFound);
        rejected.SafeReason.ShouldBe("The selected lane does not exist.");
    }

    // ---- EnforceAsync: protected-boundary edge cases the shared conformance suite does not exercise. ----

    [Fact]
    public async Task LoadAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var security = TestSecurityHarness.For(store);
        var authorized = security.Authorize(store, context, SecurityOperationKind.StateRead, SecurityEffect.Observe);
        var mismatched = new AuthorizedSessionStoreRequest<SessionOperationContext>(authorized.Request, new SessionStoreKey("some-other-store"), authorized.Grant, authorized.Intent);

        var result = await store.LoadAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task LoadAsync_WhenEnforcementIntentFenceDiffersFromRequest_ReturnsTypedFailure()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var security = TestSecurityHarness.For(store);
        var authorized = security.Authorize(store, context, SecurityOperationKind.StateRead, SecurityEffect.Observe);
        var mismatched = new AuthorizedSessionStoreRequest<SessionOperationContext>(authorized.Request, authorized.StoreKey, authorized.Grant, new SecurityEnforcementIntent(authorized.Intent.Id, new FencingToken(1)));

        var result = await store.LoadAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("The enforcement intent fence differs from the session request.");
    }

    private static AuthorizedSessionStoreRequest<TRequest> MismatchedStoreKey<TRequest>(
        InMemorySessionStore store, TRequest request, SecurityOperationKind kind, SecurityEffect effect)
        where TRequest : class
    {
        var authorized = TestSecurityHarness.For(store).Authorize(store, request, kind, effect);
        return new AuthorizedSessionStoreRequest<TRequest>(authorized.Request, new SessionStoreKey("some-other-store"), authorized.Grant, authorized.Intent);
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var (store, descriptor, context) = await SeedLaneContextAsync();
        var provision = ProvisionRequest(context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version, new SessionEntryId(Guid.NewGuid()), "enforce-provision");
        var mismatched = MismatchedStoreKey(store, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create);

        var result = await store.ProvisionLaneAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>().SafeMessage.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task ReadAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var read = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10);
        var mismatched = MismatchedStoreKey(store, read, SecurityOperationKind.StateRead, SecurityEffect.Observe);

        var result = await store.ReadAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionReadFailed>().SafeMessage.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task CreateBranchAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var branch = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("enforce-branch"));
        var mismatched = MismatchedStoreKey(store, branch, SecurityOperationKind.StateMutation, SecurityEffect.Create);

        var result = await store.CreateBranchAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionBranchFailed>().SafeMessage.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task DeleteAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var delete = new SessionDeleteRequest(context, new IdempotencyKey("enforce-delete"));
        var mismatched = MismatchedStoreKey(store, delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete);

        var result = await store.DeleteAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionDeleteFailed>().SafeMessage.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task LookupInputAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var (store, _, context) = await SeedLaneContextAsync();
        var input = new AgentInput(new InputId(Guid.NewGuid()), InputDelivery.FollowUp, [new TextPart("lookup", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var lookup = new SessionInputLookupRequest(context, input, new InputFingerprint("sha256:enforce:lookup"));
        var mismatched = MismatchedStoreKey(store, lookup, SecurityOperationKind.StateRead, SecurityEffect.Observe);

        var result = await store.LookupInputAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionInputLookupRejected>().SafeReason.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var (store, _, context, provisioned) = await ProvisionLaneAsync("enforce-admit");
        var admission = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "enforce-admit");
        var mismatched = MismatchedStoreKey(store, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append);

        var result = await store.AdmitInputAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.SafeReason.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task LoadRunStateAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(descriptor.Address, laneId, new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null), TestFactory.Identity());
        var load = new SessionRunStateRequest(context);
        var mismatched = MismatchedStoreKey(store, load, SecurityOperationKind.StateRead, SecurityEffect.Observe);

        var result = await store.LoadRunStateAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStateUnavailable>().SafeReason.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task ReleaseRunAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(descriptor.Address, laneId, new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null), TestFactory.Identity());
        var release = new SessionRunReleaseRequest(context, new OperationStateRevision(1), descriptor.Version, new IdempotencyKey("enforce-release"));
        var mismatched = MismatchedStoreKey(store, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate);

        var result = await store.ReleaseRunAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunReleaseRejected>().SafeReason.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenFencingTokenIsRequested_ReturnsRejectedForUnsupportedDistributedFencing()
    {
        var (store, _, context, provisioned, admission, accepted) = await PrepareLaneAsync("accept-fenced");
        var start = RunStartRequest(context, admission.AdmissionId, [admission.AdmissionId], accepted.Receipt.AdmittedSequence, provisioned.LaneRevision, provisioned.SessionVersion, provisioned.BranchCursor, "accept-fenced-start", expectedFencingToken: new FencingToken(1));
        var security = TestSecurityHarness.For(store);
        var authorized = security.Authorize(store, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate);
        var fenced = new AuthorizedSessionStoreRequest<SessionRunStartRequest>(authorized.Request, authorized.StoreKey, authorized.Grant, new SecurityEnforcementIntent(authorized.Intent.Id, new FencingToken(1)));

        var result = await store.AcceptRunAsync(fenced, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartRejected>().SafeReason.ShouldBe("The selected session store does not support distributed fencing.");
    }

    [Fact]
    public async Task LoadAsync_WhenGrantStoreThrowsUnexpectedException_ReturnsTypedFailure()
    {
        var security = new TestSecurityHarness();
        var throwingGrants = new ThrowingSecurityGrantStore(security);
        var store = new InMemorySessionStore(
            new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)),
            new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)),
            security, throwingGrants, TimeProvider.System);
        TestSecurityHarness.Register(store, security);
        var descriptor = await TestFactory.CreateSessionAsync(store);
        throwingGrants.ShouldThrow = true;
        var context = TestFactory.OperationContext(descriptor.Address);

        var result = await store.LoadAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("A session-store authorization prerequisite is unavailable.");
    }

    [Fact]
    public async Task CreateAsync_WhenCoreOperationThrowsAfterSuccessfulAuthorization_PropagatesAndReportsFaulted()
    {
        var clock = new ThrowsAfterTimeProvider(TimeProvider.System, throwOnCall: 2);
        var security = new TestSecurityHarness();
        var logger = new RecordingSessionStoreLogger();
        var store = new InMemorySessionStore(
            new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)),
            new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)),
            security, security, clock, logger);
        TestSecurityHarness.Register(store, security);
        var request = TestFactory.CreateRequest();

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Simulated clock failure.");
        logger.Snapshot().ShouldContain(static item => item.EventId.Id == 16001);
    }

    /// <summary>Forwards to an authoritative grant store but can throw on the intent-aware overload to exercise EnforceAsync's catch-all path.</summary>
    private sealed class ThrowingSecurityGrantStore(ISecurityGrantStore inner): ISecurityGrantStore
    {
        public bool ShouldThrow { get; set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
            inner.RegisterAsync(grant, cancellationToken);

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) =>
            inner.ValidateAndConsumeAsync(grant, enforcement, cancellationToken);

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default) =>
            ShouldThrow
                ? throw new InvalidOperationException("Simulated grant-store failure.")
                : inner.ValidateAndConsumeAsync(grant, enforcement, intent, cancellationToken);

        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevocationNotFound(grantId));
        }
    }

    /// <summary>A clock that throws starting from a configured call number, to exercise a post-authorization core-operation failure.</summary>
    private sealed class ThrowsAfterTimeProvider(TimeProvider inner, int throwOnCall): TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => Interlocked.Increment(ref _calls) >= throwOnCall
            ? throw new InvalidOperationException("Simulated clock failure.")
            : inner.GetUtcNow();
    }
}
