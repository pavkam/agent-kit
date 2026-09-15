// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using System.Text.Json;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;
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

    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionOperationContext Context)> SeedAsync(int entryCount)
    {
        var store = TestFactory.CreateStore();
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

    [Fact]
    public async Task ReadAsync_WhenPageSizeSmallerThanTotal_ReturnsPartialPageWithHasMoreTrue()
    {
        var (store, descriptor, context) = await SeedAsync(5);
        var result = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2), TestContext.Current.CancellationToken);
        var page = result.ShouldBeOfType<SessionPage>();
        page.Entries.Length.ShouldBe(2);
        page.HasMore.ShouldBeTrue();
        page.ThroughSequence.Value.ShouldBe(2);
    }

    [Fact]
    public async Task ReadAsync_WhenContinuingFromPreviousPage_ReturnsRemainingEntries()
    {
        var (store, descriptor, context) = await SeedAsync(5);
        var firstPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2), TestContext.Current.CancellationToken);
        var secondPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 2), TestContext.Current.CancellationToken);
        secondPage.Entries.Length.ShouldBe(2);
        secondPage.HasMore.ShouldBeTrue();
        secondPage.ThroughSequence.Value.ShouldBe(4);
    }

    [Fact]
    public async Task ReadAsync_WhenAppendOccursBetweenPages_ContinuationRetainsOriginalPrefix()
    {
        var (store, descriptor, context) = await SeedAsync(3);
        var firstPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2), TestContext.Current.CancellationToken);
        var appendedEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 4, "later");
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(3), new IdempotencyKey("later"), [appendedEntry]), TestContext.Current.CancellationToken);

        var issued = firstPage.Snapshot!;
        var reconstructed = new SessionReadSnapshot(issued.Address, issued.BranchId, issued.Version, issued.UpperSequence);
        var secondPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 10, reconstructed), TestContext.Current.CancellationToken);

        secondPage.Snapshot.ShouldBe(reconstructed);
        secondPage.Entries.ShouldHaveSingleItem().Sequence.ShouldBe(new SessionSequence(3));
        secondPage.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenOneAppendCommitsMultipleEntries_PreservesDistinctVersionAndUpperSequence()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "one"),
            TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 2, "two"));
        var appended = (SessionAppended) await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("batch"), entries), TestContext.Current.CancellationToken);

        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);

        appended.NewVersion.ShouldBe(new SessionVersion(1));
        page.Snapshot.ShouldNotBeNull().Version.ShouldBe(new SessionVersion(1));
        page.Snapshot.UpperSequence.ShouldBe(new SessionSequence(2));
    }

    [Fact]
    public async Task ReadAsync_WhenBranchIsEmpty_ReturnsEmptyPageWithHasMoreFalse()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var result = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        var page = result.ShouldBeOfType<SessionPage>();
        page.Entries.ShouldBeEmpty();
        page.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenNewCursorIsBeyondBranchTip_ReturnsTypedFailure()
    {
        var (store, descriptor, context) = await SeedAsync(1);
        var result = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), 10), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionReadFailed>();
    }

    [Fact]
    public async Task ReadAsync_WhenSnapshotClaimsFutureState_ReturnsTypedFailure()
    {
        var (store, descriptor, context) = await SeedAsync(1);
        var snapshot = new SessionReadSnapshot(descriptor.Address, descriptor.ActiveBranchId, new SessionVersion(9), new SessionSequence(9));
        var result = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10, snapshot), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionReadFailed>();
    }

    [Fact]
    public async Task ReadAsync_WhenBranchDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var result = await store.ReadAsync(new SessionReadRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionReadNotFound>();
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

    [Fact]
    public async Task AppendAsync_WhenAppendedTwiceInSequence_OrdersEntriesByAppendOrder()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var first = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "first");
        var second = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 2, "second");
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("a1"), [first]), TestContext.Current.CancellationToken);
        var secondResult = (SessionAppended) await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(1), new IdempotencyKey("a2"), [second]), TestContext.Current.CancellationToken);
        secondResult.NewVersion.Value.ShouldBe(2);
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.Length.ShouldBe(2);
        ((MessageSessionEntry) page.Entries[0]).Message.Parts.OfType<TextPart>().Single().Text.ShouldBe("first");
        ((MessageSessionEntry) page.Entries[1]).Message.Parts.OfType<TextPart>().Single().Text.ShouldBe("second");
    }

    private static ContentPart JsonPart(bool toolArguments, ToolCallId callId, string target)
    {
        using var document = JsonDocument.Parse($$"""{"target":"{{target}}"}""");
        var value = document.RootElement.Clone();
        return toolArguments ? new ToolCallPart(callId, new ToolReference(new ToolId("test"), null, "test"), value, null, ExtensionData.Empty) : new StructuredDataPart(value, null, ExtensionData.Empty);
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
    public async Task AppendAsync_WhenRetriedWithSameIdempotencyKey_DoesNotDuplicateEntries()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("dupe"), [entry]);
        var first = (SessionAppended) await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var second = (SessionAppended) await store.AppendAsync(request, TestContext.Current.CancellationToken);
        second.NewVersion.ShouldBe(first.NewVersion);
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
    public async Task AppendAsync_ConcurrentAppendsAtSameExpectedVersion_YieldsOneSuccessAndOneConflict()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entryA = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "a");
        var entryB = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "b");
        var requestA = new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("racer-a"), [entryA]);
        var requestB = new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("racer-b"), [entryB]);
        var results = await Task.WhenAll(store.AppendAsync(requestA, TestContext.Current.CancellationToken).AsTask(), store.AppendAsync(requestB, TestContext.Current.CancellationToken).AsTask());
        results.OfType<SessionAppended>().Count().ShouldBe(1);
        results.OfType<SessionAppendConflict>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenForkingMidway_CreatesBranchWithOnlyEntriesUpToForkPoint()
    {
        var (store, descriptor, context) = await SeedAsync(4);
        var result = await store.CreateBranchAsync(new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), new IdempotencyKey("b1")), TestContext.Current.CancellationToken);
        var branched = result.ShouldBeOfType<SessionBranched>();
        var branchContext = TestFactory.OperationContext(descriptor.Address);
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(branchContext, branched.NewBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.Length.ShouldBe(2);
    }

    [Fact]
    public async Task CreateBranchAsync_LeavesOriginalBranchUnchanged()
    {
        // Four seeded appends leave version 4 and sequence 4; branching advances the version to 5.
        var (store, descriptor, context) = await SeedAsync(4);
        var branched = (SessionBranched) await store.CreateBranchAsync(new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), new IdempotencyKey("b1")), TestContext.Current.CancellationToken);
        var newEntry = TestFactory.MessageEntry(descriptor.Address, branched.NewBranchId, 5, "new-branch-only");

        var appended = await store.AppendAsync(new SessionAppendRequest(context, branched.NewBranchId, new SessionVersion(5), new IdempotencyKey("nb1"), [newEntry]), TestContext.Current.CancellationToken);
        var originalPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        var branchPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, branched.NewBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);

        appended.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(new SessionVersion(6));
        originalPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2, 3, 4]);
        branchPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2, 5]);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenRetriedWithSameIdempotencyKey_ReturnsSameBranch()
    {
        var (store, descriptor, context) = await SeedAsync(2);
        var request = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(1), new IdempotencyKey("dupe"));
        var first = (SessionBranched) await store.CreateBranchAsync(request, TestContext.Current.CancellationToken);
        var second = (SessionBranched) await store.CreateBranchAsync(request, TestContext.Current.CancellationToken);
        second.NewBranchId.ShouldBe(first.NewBranchId);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenReplayCarriesChangedForkPoint_RejectsAndPreservesOriginalReceipt()
    {
        var (store, descriptor, context) = await SeedAsync(2);
        var key = new IdempotencyKey("branch-evidence");
        var first = (SessionBranched) await store.CreateBranchAsync(new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(1), key), TestContext.Current.CancellationToken);
        var changed = await store.CreateBranchAsync(new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), key), TestContext.Current.CancellationToken);
        _ = changed.ShouldBeOfType<SessionBranchFailed>();
        first.ForkedAtSequence.ShouldBe(new SessionSequence(1));
    }

    [Fact]
    public async Task CreateBranchAsync_WhenForkPointExceedsParentLength_ReturnsParentNotFound()
    {
        var (store, descriptor, context) = await SeedAsync(2);
        var result = await store.CreateBranchAsync(new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(10), new IdempotencyKey("b1")), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionBranchParentNotFound>();
    }

    [Fact]
    public async Task CreateBranchAsync_WhenParentBranchDoesNotExist_ReturnsParentNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var result = await store.CreateBranchAsync(new SessionBranchRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), new IdempotencyKey("b1")), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionBranchParentNotFound>();
    }

    [Fact]
    public async Task CreateBranchAsync_WhenForkingAtZero_CreatesEmptyBranch()
    {
        var (store, descriptor, context) = await SeedAsync(3);
        var branched = (SessionBranched) await store.CreateBranchAsync(new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("b1")), TestContext.Current.CancellationToken);
        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, branched.NewBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.ShouldBeEmpty();
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

    [Fact]
    public async Task CreateAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var store = TestFactory.CreateStore();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.CreateAsync(TestFactory.CreateRequest(), cts.Token));
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
}
