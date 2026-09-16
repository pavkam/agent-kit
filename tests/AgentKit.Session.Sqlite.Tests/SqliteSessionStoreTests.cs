// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using System.Text.Json;

using AgentKit.Conformance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Runs the reusable protected session-store contract against durable SQLite.</summary>
public sealed class SqliteSessionStoreTests: SessionStoreConformanceTests<SqliteSessionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override SqliteSessionStoreConformanceFixture CreateFixture() => new();

    // ---- Probing tests beyond the shared suite. ----

    [Fact]
    public async Task AppendAsync_WhenStoreIsReopened_ReturnsCommittedEntriesAndVersion()
    {
        // sessions-persistence-and-branching.md: SQLite "additionally proves committed state survives close and reopen".
        using var directory = new TempDirectory();
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        SessionDescriptor descriptor;
        MessageSessionEntry entry;
        SessionOperationContext context;

        await using (var first = Harness.Open(directory.Path, instance))
        {
            descriptor = await first.CreateSessionAsync();
            context = Harness.SessionContext(descriptor.Address, 20);
            entry = Harness.MessageEntry(descriptor, 30, 1, "durable");
            var appended = await first.Store.AppendAsync(
                await first.AuthorizeAsync(
                    new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("a1"), [entry]),
                    SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken);
            _ = appended.ShouldBeOfType<SessionAppended>();
        }

        await using var second = Harness.Open(directory.Path, instance);
        var loaded = await second.Store.LoadAsync(
            await second.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = await second.Store.ReadAsync(
            await second.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
        page.ShouldBeOfType<SessionPage>().Entries.ShouldBe([entry]);
    }

    [Fact]
    public async Task AppendAsync_WhenEquivalentRequestWithToolCallPartReplays_ReturnsOriginalReceipt()
    {
        // Idempotent replay must compare canonical content; a JsonElement-bearing part must not defeat replay equality.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var entry = Harness.AssistantToolCallEntry(descriptor, 30, 1);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("tool-call"), [entry]);

        var first = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var replay = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = first.ShouldBeOfType<SessionAppended>((first as SessionAppendFailed)?.SafeMessage);
        replay.ShouldBeOfType<SessionAppended>((replay as SessionAppendFailed)?.SafeMessage).NewVersion.ShouldBe(((SessionAppended) first).NewVersion);
    }

    [Fact]
    public async Task AppendAsync_WhenEquivalentToolCallRequestReplaysAfterReopen_ReturnsOriginalReceipt()
    {
        using var directory = new TempDirectory();
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        SessionDescriptor descriptor;
        SessionAppendRequest request;
        SessionAppended first;

        await using (var opened = Harness.Open(directory.Path, instance))
        {
            descriptor = await opened.CreateSessionAsync();
            var context = Harness.SessionContext(descriptor.Address, 20);
            request = new SessionAppendRequest(
                context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("tool-call"),
                [Harness.AssistantToolCallEntry(descriptor, 30, 1)]);
            first = (await opened.Store.AppendAsync(
                await opened.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        }

        await using var reopened = Harness.Open(directory.Path, instance);
        var replay = await reopened.Store.AppendAsync(
            await reopened.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        replay.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(first.NewVersion);
    }

    [Fact]
    public async Task AppendAsync_WhenBatchCommitsMultipleEntries_NextSingleAppendRequiresNextSequenceNotVersionPlusOne()
    {
        // Documents the store rule callers must follow: sequence continuity is whole-session, not Version + 1.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var batch = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("batch"),
            [Harness.MessageEntry(descriptor, 30, 1, "a"), Harness.MessageEntry(descriptor, 32, 2, "b")]);
        var batched = (await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(batch, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();

        // A caller that follows the "Version + 1" convention used by Plan/Compaction/loop-rebase computes sequence 2.
        var versionDerived = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, batched.NewVersion, new IdempotencyKey("version-derived"),
            [Harness.MessageEntry(descriptor, 34, batched.NewVersion.Value + 1, "c")]);
        var result = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(versionDerived, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        // Both first-party stores reject it; this pins the contract so caller bugs are visible.
        batched.NewVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
        _ = result.ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenEntryHasNoCodec_ReturnsTypedFailureWithoutMutation()
    {
        // An entry without a durable codec must be rejected as a typed store outcome before any state changes.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var uncoded = new UncodedSessionEntry(
            new SessionEntryId(new Guid(99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), descriptor.Address,
            new InRunOperationCorrelation(new OperationId(new Guid(98, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), new RunId(new Guid(97, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), null),
            descriptor.ActiveBranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"));
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("uncoded"), [uncoded]);

        var result = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var loaded = await harness.Store.LoadAsync(
            await harness.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = await harness.Store.ReadAsync(
            await harness.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldContain("durable codec");
        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(descriptor.Version);
        page.ShouldBeOfType<SessionPage>().Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Operations_WhenStoreIsDisposed_ThrowObjectDisposedExceptionWithoutTouchingDatabase()
    {
        // Disposal releases the process gate; every protected operation fails closed afterward and persisted state is untouched.
        using var directory = new TempDirectory();
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        SessionDescriptor descriptor;
        SessionOperationContext context;
        await using (var first = Harness.Open(directory.Path, instance))
        {
            descriptor = await first.CreateSessionAsync();
            context = Harness.SessionContext(descriptor.Address, 20);
            var append = new SessionAppendRequest(
                context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("late"),
                [Harness.MessageEntry(descriptor, 30, 1, "late")]);
            var authorized = await first.AuthorizeAsync(append, SecurityOperationKind.StateMutation, SecurityEffect.Append);
            first.Store.Dispose();
            first.Store.Dispose();

            _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
                await first.Store.AppendAsync(authorized, TestContext.Current.CancellationToken));
            _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
                await first.Store.LoadAsync(
                    await first.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
                    TestContext.Current.CancellationToken));
        }

        await using var reopened = Harness.Open(directory.Path, instance);
        var loaded = await reopened.Store.LoadAsync(
            await reopened.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(descriptor.Version);
    }

    [Fact]
    public async Task ReadAsync_WhenIssuedSnapshotsExceedConfiguredBound_OldestContinuationBecomesUnavailable()
    {
        // Class remarks: at most MaximumIssuedReadSnapshots distinct snapshots are retained; eviction fails the continuation.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), 1_048_576, maximumIssuedReadSnapshots: 1));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var first = await ReadFirstPageAsync(harness, descriptor, context);
        await AppendAsync(harness, descriptor, context, descriptor.Version, "a1", Harness.MessageEntry(descriptor, 30, 1, "one"));
        var second = await ReadFirstPageAsync(harness, descriptor, context);

        var continued = await harness.Store.ReadAsync(
            await harness.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100, first.Snapshot!),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var latest = await harness.Store.ReadAsync(
            await harness.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100, second.Snapshot!),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        second.Snapshot.ShouldNotBe(first.Snapshot);
        continued.ShouldBeOfType<SessionReadFailed>().SafeMessage.ShouldBe("The supplied session read snapshot is not available for this branch.");
        _ = latest.ShouldBeOfType<SessionPage>();
    }

    [Fact]
    public async Task ReadAsync_WhenIssuedSnapshotsStayWithinConfiguredBound_OlderContinuationRemainsAvailable()
    {
        // Contrast case: the same sequence with a bound of two keeps the first snapshot honoured.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), 1_048_576, maximumIssuedReadSnapshots: 2));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var first = await ReadFirstPageAsync(harness, descriptor, context);
        await AppendAsync(harness, descriptor, context, descriptor.Version, "a1", Harness.MessageEntry(descriptor, 30, 1, "one"));
        _ = await ReadFirstPageAsync(harness, descriptor, context);

        var continued = await harness.Store.ReadAsync(
            await harness.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100, first.Snapshot!),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        // The pinned prefix is empty because the first snapshot was taken before the append.
        continued.ShouldBeOfType<SessionPage>().Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenEncodedEntryExceedsMaximumEntryPayloadBytes_RejectsWithoutPersisting()
    {
        // Class remarks: an entry whose encoded payload exceeds MaximumEntryPayloadBytes is rejected and nothing is persisted.
        using var directory = new TempDirectory();
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        // A minimal message entry encodes to roughly 1 KiB; the bound admits it and rejects the padded one.
        var settings = new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), maximumEntryPayloadBytes: 2_048);
        SessionDescriptor descriptor;
        SessionOperationContext context;

        await using (var harness = Harness.Open(directory.Path, instance, settings))
        {
            descriptor = await harness.CreateSessionAsync();
            context = Harness.SessionContext(descriptor.Address, 20);
            var small = Harness.MessageEntry(descriptor, 30, 1, "ok");
            var oversized = Harness.MessageEntry(descriptor, 32, 2, new string('x', 4_096));

            var rejected = await harness.Store.AppendAsync(
                await harness.AuthorizeAsync(
                    new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("big"), [small, oversized]),
                    SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken);
            var loaded = await harness.Store.LoadAsync(
                await harness.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
                TestContext.Current.CancellationToken);

            rejected.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldMatch(@"^Entry at position 1 encodes to \d+ bytes; the selected session store accepts at most 2048\.$");
            loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(descriptor.Version);
        }

        await using var reopened = Harness.Open(directory.Path, instance, settings);
        var page = await reopened.Store.ReadAsync(
            await reopened.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        page.ShouldBeOfType<SessionPage>().Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenEncodedEntryFitsMaximumEntryPayloadBytes_Commits()
    {
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), maximumEntryPayloadBytes: 4_096));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);

        var appended = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(
                new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("fits"),
                    [Harness.MessageEntry(descriptor, 30, 1, "small enough")]),
                SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        appended.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
    }

    [Fact]
    public async Task AppendAsync_WhenStoreIsRegisteredThroughConfigureDelegate_EnforcesConfiguredPayloadBound()
    {
        // The configure overload must bind the same effective settings as the explicit settings overload.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            static options =>
            {
                options.LockTimeout = TimeSpan.FromSeconds(1);
                options.MaximumEntryPayloadBytes = 2_048;
            });
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);

        var rejected = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(
                new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("big"),
                    [Harness.MessageEntry(descriptor, 30, 1, new string('x', 4_096))]),
                SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var loaded = await harness.Store.LoadAsync(
            await harness.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        rejected.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldMatch(@"^Entry at position 0 encodes to \d+ bytes; the selected session store accepts at most 2048\.$");
        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(descriptor.Version);
    }

    // ---- Delete and deleted-create-retry coverage, using the reusable conformance fixture directly. ----

    [Fact]
    public async Task DeleteAsync_WhenSessionExists_RemovesAllRowsAndReturnsDeleted()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store);
        var context = Coverage.SessionContext(descriptor.Address, 900);
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("pre-delete"),
            [Coverage.MessageEntry(descriptor, 901, 1, "before-delete")]);
        _ = (await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        var delete = Coverage.DeleteRequest(context, "delete-1");

        var result = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(fixture, delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(
            await Coverage.AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDeleted(descriptor.Address));
        _ = loaded.ShouldBeOfType<SessionNotFound>();
    }

    [Fact]
    public async Task DeleteAsync_WhenRetriedWithSameIdempotencyKey_ReturnsSameReceipt()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store);
        var context = Coverage.SessionContext(descriptor.Address, 910);
        var delete = Coverage.DeleteRequest(context, "delete-retry");

        var first = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(fixture, delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);
        var second = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(fixture, delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);

        first.ShouldBe(new SessionDeleted(descriptor.Address));
        second.ShouldBe(new SessionDeleted(descriptor.Address));
    }

    [Fact]
    public async Task DeleteAsync_WhenRetriedWithDifferentEvidence_ReturnsTypedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store);
        var context = Coverage.SessionContext(descriptor.Address, 920);
        var otherContext = Coverage.SessionContext(descriptor.Address, 930);
        var first = Coverage.DeleteRequest(context, "delete-evidence");
        var second = Coverage.DeleteRequest(otherContext, "delete-evidence");

        _ = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(fixture, first, SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);
        var result = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(fixture, second, SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionDeleteFailed>().SafeMessage
            .ShouldBe("The idempotency key was previously used with different request evidence.");
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionNeverExisted_ReturnsDeletedWithoutSideEffects()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var context = Coverage.SessionContext(address, 940);
        var delete = Coverage.DeleteRequest(context, "delete-missing");

        var result = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(fixture, delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDeleted(address));
    }

    [Fact]
    public async Task DeleteAsync_WhenCallerTenantDiffersFromOwner_MasksExistenceAndDoesNotDelete()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store);
        var foreignIdentity = Coverage.Identity("tenant-foreign", "foreign-user");
        var foreignContext = Coverage.SessionContext(descriptor.Address, 950, foreignIdentity);
        var delete = Coverage.DeleteRequest(foreignContext, "delete-foreign");

        var result = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(fixture, delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);
        var ownerContext = Coverage.SessionContext(descriptor.Address, 960);
        var loaded = await store.LoadAsync(
            await Coverage.AuthorizeAsync(fixture, ownerContext, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDeleted(descriptor.Address));
        _ = loaded.ShouldBeOfType<SessionLoaded>();
    }

    [Fact]
    public async Task CreateAsync_WhenRetriedAfterDeleteWithSameEvidence_ReturnsDeletedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = Coverage.CreateStoreRequest("create-then-delete");
        var created = await store.CreateAsync(
            await Coverage.AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var descriptor = created.ShouldBeOfType<SessionCreated>().Descriptor;
        var deleteContext = Coverage.SessionContext(descriptor.Address, 970);
        _ = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(
                fixture, Coverage.DeleteRequest(deleteContext, "delete-after-create"),
                SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);

        var replay = await store.CreateAsync(
            await Coverage.AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        replay.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("The session created by this idempotency key was deleted.");
    }

    [Fact]
    public async Task CreateAsync_WhenRetriedAfterDeleteWithDifferentEvidence_ReturnsTypedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = Coverage.CreateStoreRequest("create-then-delete-2");
        var created = await store.CreateAsync(
            await Coverage.AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var descriptor = created.ShouldBeOfType<SessionCreated>().Descriptor;
        var deleteContext = Coverage.SessionContext(descriptor.Address, 980);
        _ = await store.DeleteAsync(
            await Coverage.AuthorizeAsync(
                fixture, Coverage.DeleteRequest(deleteContext, "delete-after-create-2"),
                SecurityOperationKind.StateMutation, SecurityEffect.Delete),
            TestContext.Current.CancellationToken);
        var differentConversation = Coverage.CreateStoreRequest(
            "create-then-delete-2", new ConversationId(Guid.Parse("99999999-9999-9999-9999-999999999999")));

        var replay = await store.CreateAsync(
            await Coverage.AuthorizeAsync(fixture, differentConversation, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        replay.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("The idempotency key was previously used with different request evidence.");
    }

    [Fact]
    public async Task CreateAsync_WhenAddressAlreadyPresentUnderDifferentIdempotencyKey_ReturnsTypedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await Coverage.CreateSessionAsync(fixture, store, "first-key");

        var second = await store.CreateAsync(
            await Coverage.AuthorizeAsync(
                fixture, Coverage.CreateStoreRequest("second-key"), SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        second.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("The allocated session address is already present.");
    }

    private static async Task<SessionPage> ReadFirstPageAsync(Harness harness, SessionDescriptor descriptor, SessionOperationContext context)
    {
        var result = await harness.Store.ReadAsync(
            await harness.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = result.ShouldBeOfType<SessionPage>();
        _ = page.Snapshot.ShouldNotBeNull();
        return page;
    }

    private static async Task AppendAsync(Harness harness, SessionDescriptor descriptor, SessionOperationContext context,
        SessionVersion expectedVersion, string idempotencyKey, MessageSessionEntry entry)
    {
        var result = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(
                new SessionAppendRequest(context, descriptor.ActiveBranchId, expectedVersion, new IdempotencyKey(idempotencyKey), [entry]),
                SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionAppended>((result as SessionAppendFailed)?.SafeMessage);
    }

    /// <summary>A session entry kind that no registered codec can encode.</summary>
    private sealed record UncodedSessionEntry(
        SessionEntryId Id, SessionAddress Address, OperationCorrelation Correlation, BranchId BranchId,
        SessionSequence Sequence, SessionEntryId? CausalParentId, DateTimeOffset RecordedAt, SchemaVersion SchemaVersion)
        : SessionEntry(Id, Address, Correlation, BranchId, Sequence, CausalParentId, RecordedAt, SchemaVersion);

    private sealed class TempDirectory: IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agentkit-session-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// Construction helpers for coverage-focused tests that exercise <see cref="SqliteSessionStoreConformanceFixture"/>
    /// directly instead of the file-reopening <see cref="Harness"/>, mirroring the shared conformance suite's own
    /// private helpers so lane, admission, and run-acceptance requests can be built for edge cases the shared suite
    /// does not exercise.
    /// </summary>
    private static class Coverage
    {
        public static ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
            SqliteSessionStoreConformanceFixture fixture, TRequest request, SecurityOperationKind kind, SecurityEffect effect)
            where TRequest : class =>
            fixture.AuthorizeAsync(request, kind, effect, TestContext.Current.CancellationToken);

        public static async ValueTask<SessionDescriptor> CreateSessionAsync(
            SqliteSessionStoreConformanceFixture fixture, ISessionStore store, string idempotencyKey = "create")
        {
            var request = CreateStoreRequest(idempotencyKey);
            var result = await store.CreateAsync(
                await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
                TestContext.Current.CancellationToken);
            return result.ShouldBeOfType<SessionCreated>().Descriptor;
        }

        public static SessionStoreCreateRequest CreateStoreRequest(string idempotencyKey, ConversationId? conversationId = null)
        {
            var agentId = Identifier<AgentId>(1);
            var identity = Identity();
            var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
            var authorization = Authorization(agentId, null, correlation, identity);
            var logical = new SessionCreateRequest(
                agentId, identity, authorization, conversationId ?? Identifier<ConversationId>(3),
                new IdempotencyKey(idempotencyKey), ExtensionData.Empty);
            var address = new SessionAddress(agentId, Identifier<SessionId>(4));
            var context = new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));
            return new SessionStoreCreateRequest(logical, address, context);
        }

        public static SessionOperationContext SessionContext(SessionAddress address, int offset, ExecutionIdentity? identity = null)
        {
            var used = identity ?? Identity();
            var correlation = Correlation(offset);
            return new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, used,
                Authorization(address.AgentId, address.SessionId, correlation, used));
        }

        public static SessionOperationContext LaneContext(
            SessionAddress address, ExecutionLaneId laneId, ExecutionIdentity identity, OperationCorrelation correlation) =>
            new(address.AgentId, address.SessionId, laneId, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));

        public static SessionDeleteRequest DeleteRequest(SessionOperationContext context, string idempotencyKey) =>
            new(context, new IdempotencyKey(idempotencyKey));

        public static MessageSessionEntry MessageEntry(
            SessionDescriptor descriptor, int offset, long sequence, string text, BranchId? branchId = null)
        {
            var correlation = Correlation(offset);
            var branch = branchId ?? descriptor.ActiveBranchId;
            var message = new UserMessage(
                Identifier<MessageId>(offset + 1), descriptor.Address.AgentId,
                descriptor.Address.SessionId, descriptor.ConversationId, branch,
                correlation.RunId, null, Timestamp(offset), MessageState.Complete,
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
            return new MessageSessionEntry(
                Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation,
                branch, new SessionSequence(sequence), null, Timestamp(offset),
                new SchemaVersion("1"), message);
        }

        public static SessionProfileReference Profile() => new(new SessionProfileKey("coverage"), new SessionProfileVersion(1));

        public static RunConfigurationReference Configuration() =>
            new(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:coverage-configuration"));

        public static SessionExecutionLaneProvisionRequest ProvisionRequest(
            SessionOperationContext context, SessionBranchCursor branchCursor, SessionVersion expectedVersion,
            SessionEntryId entryId, int offset, string idempotencyKey) =>
            new(context, branchCursor, expectedVersion, entryId, Profile(), Configuration(), Timestamp(offset),
                new IdempotencyKey(idempotencyKey));

        public static SessionInputAdmissionRequest AdmissionRequest(
            SessionOperationContext context, AdmissionId admissionId, InputId inputId, SessionEntryId entryId,
            SessionVersion version, SessionLaneRevision laneRevision, SessionBranchCursor cursor, string key,
            int maximumPendingInputs = 8)
        {
            var original = new AgentInput(
                inputId, InputDelivery.FollowUp,
                [new TextPart($"{key}-original", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
            var effective = new AgentInput(
                inputId, InputDelivery.FollowUp,
                [new TextPart($"{key}-effective", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
            var preprocessing = new InputPreprocessingManifest(
                new ConfigurationVersion(1), new InputFingerprint($"sha256:{key}:original"),
                new InputFingerprint($"sha256:{key}:effective"));
            return new SessionInputAdmissionRequest(
                context, admissionId, entryId, original, effective, preprocessing,
                Timestamp(entryId.Value.GetHashCode()), version, laneRevision, cursor,
                new IdempotencyKey($"{key}-admit"), maximumPendingInputs);
        }

        public static async ValueTask<(
            SessionDescriptor Descriptor, SessionOperationContext Context, SessionExecutionLaneProvisioned Provisioned,
            SessionInputAdmissionRequest Admission, AcceptedInput Accepted)> ProvisionAndAdmitAsync(
            SqliteSessionStoreConformanceFixture fixture, ISessionStore store, int offset, string key)
        {
            var descriptor = await CreateSessionAsync(fixture, store, $"{key}-create");
            var identity = Identity();
            var laneId = Identifier<ExecutionLaneId>(offset);
            var context = LaneContext(
                descriptor.Address, laneId, identity, new BeforeRunOperationCorrelation(Identifier<OperationId>(offset + 1), null));
            var provision = ProvisionRequest(
                context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
                Identifier<SessionEntryId>(offset + 2), offset, $"{key}-provision");
            var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
                await AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
                TestContext.Current.CancellationToken);
            var admission = AdmissionRequest(
                context, Identifier<AdmissionId>(offset + 3), Identifier<InputId>(offset + 4),
                Identifier<SessionEntryId>(offset + 5), provisioned.SessionVersion, provisioned.LaneRevision,
                provisioned.BranchCursor, key);
            var accepted = (AcceptedInput) await store.AdmitInputAsync(
                await AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken);
            return (descriptor, context, provisioned, admission, accepted);
        }

        public static SessionRunStartRequest StartRequest(
            (SessionDescriptor Descriptor, SessionOperationContext Context, SessionExecutionLaneProvisioned Provisioned,
                SessionInputAdmissionRequest Admission, AcceptedInput Accepted) prepared,
            int offset,
            SessionOperationContext? context = null,
            SessionVersion? expectedVersion = null,
            SessionBranchCursor? branchCursor = null,
            SessionLaneRevision? expectedLaneRevision = null,
            ImmutableArray<AdmissionId>? selectedAdmissionIds = null,
            AdmissionId? initiatingAdmissionId = null,
            ImmutableArray<SessionEntryId>? entryIds = null,
            ImmutableArray<MessageId>? messageIds = null,
            string idempotencyKey = "accept")
        {
            var usedContext = context ?? prepared.Context;
            var runId = Identifier<RunId>(offset);
            var turnId = Identifier<TurnId>(offset + 1);
            var inRunCorrelation = new InRunOperationCorrelation(usedContext.Correlation.OperationId, runId, turnId);
            var inRunAuthorization = Authorization(
                prepared.Descriptor.Address.AgentId, prepared.Descriptor.Address.SessionId, inRunCorrelation, usedContext.Identity);
            return new SessionRunStartRequest(
                usedContext, initiatingAdmissionId ?? prepared.Admission.AdmissionId,
                selectedAdmissionIds ?? [prepared.Admission.AdmissionId],
                prepared.Accepted.Receipt.AdmittedSequence,
                expectedLaneRevision ?? new SessionLaneRevision(prepared.Provisioned.LaneRevision.Value + 1),
                expectedVersion ?? new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1),
                branchCursor ?? new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, prepared.Admission.EntryId),
                null, runId, turnId, Identifier<SessionEntryId>(offset + 2),
                entryIds ?? [Identifier<SessionEntryId>(offset + 3)], messageIds ?? [Identifier<MessageId>(offset + 4)],
                Identifier<SessionEntryId>(offset + 5), new OperationStateRevision(1),
                Profile(), Configuration(), inRunAuthorization, Timestamp(offset), new IdempotencyKey(idempotencyKey));
        }

        public static SessionRunReleaseRequest ReleaseRequest(
            SessionOperationContext context, OperationStateRevision expectedStateRevision, SessionVersion expectedVersion,
            string idempotencyKey) =>
            new(context, expectedStateRevision, expectedVersion, new IdempotencyKey(idempotencyKey));

        public static SecurityAuthorizationContext Authorization(
            AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
            new(new SecurityProfileKey("coverage"), new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:coverage-policy")),
                new ComponentKey<ISecurityAuthority>("coverage"), new AgentDefinitionRevision(1),
                new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

        public static ExecutionIdentity Identity(string tenant = "tenant-owner", string principal = "owner") =>
            TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

        public static InRunOperationCorrelation Correlation(int offset) =>
            new(Identifier<OperationId>(offset), Identifier<RunId>(offset + 1), null);

        public static DateTimeOffset Timestamp(int offset) => DateTimeOffset.UnixEpoch.AddSeconds(Math.Abs((long) offset) + 1);

        public static T Identifier<T>(int value)
        {
            var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
            return typeof(T) switch
            {
                var t when t == typeof(AgentId) => (T) (object) new AgentId(guid),
                var t when t == typeof(SessionId) => (T) (object) new SessionId(guid),
                var t when t == typeof(ConversationId) => (T) (object) new ConversationId(guid),
                var t when t == typeof(OperationId) => (T) (object) new OperationId(guid),
                var t when t == typeof(RunId) => (T) (object) new RunId(guid),
                var t when t == typeof(TurnId) => (T) (object) new TurnId(guid),
                var t when t == typeof(MessageId) => (T) (object) new MessageId(guid),
                var t when t == typeof(SessionEntryId) => (T) (object) new SessionEntryId(guid),
                var t when t == typeof(ExecutionLaneId) => (T) (object) new ExecutionLaneId(guid),
                var t when t == typeof(AdmissionId) => (T) (object) new AdmissionId(guid),
                var t when t == typeof(InputId) => (T) (object) new InputId(guid),
                var t when t == typeof(SecurityPolicySnapshotId) => (T) (object) new SecurityPolicySnapshotId(guid),
                _ => throw new NotSupportedException(typeof(T).Name),
            };
        }
    }

    private sealed class Harness: ISecurityAuditDispatcher, IAsyncDisposable
    {
        private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        private readonly ServiceProvider _services;
        private readonly InMemorySecurityGrantStore _grants;
        private long _nextIdentity;

        private Harness(string directory, SqliteSessionStoreInstanceId instance, SqliteSessionStoreSettings? settings,
            Action<SqliteSessionStoreOptions>? configure)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton<TimeProvider>(_timeProvider);
            _grants = new InMemorySecurityGrantStore(_timeProvider);
            _ = services.AddSingleton<ISecurityGrantStore>(_grants);
            _ = services.AddSingleton<ISecurityAuditDispatcher>(this);
            var target = new SqliteSessionStoreTarget(
                Path.Combine(directory, "sessions.db"), instance,
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
            _ = configure is null
                ? services.AddSqliteSessionStore(target, settings)
                : services.AddSqliteSessionStore(target, configure);
            _services = services.BuildServiceProvider();
            Store = (SqliteSessionStore) _services.GetRequiredService<ISessionStore>();
        }

        public SqliteSessionStore Store { get; }

        public static Harness Open(string directory, SqliteSessionStoreInstanceId instance, SqliteSessionStoreSettings? settings = null) =>
            new(directory, instance, settings, configure: null);

        /// <summary>Opens the store through the configure-delegate registration overload.</summary>
        public static Harness Open(string directory, SqliteSessionStoreInstanceId instance, Action<SqliteSessionStoreOptions> configure) =>
            new(directory, instance, settings: null, configure);

        public async ValueTask<SessionDescriptor> CreateSessionAsync()
        {
            var request = CreateStoreRequest();
            var result = await Store.CreateAsync(
                await AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
                TestContext.Current.CancellationToken);
            return result.ShouldBeOfType<SessionCreated>().Descriptor;
        }

        public async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
            TRequest request, SecurityOperationKind kind, SecurityEffect effect)
            where TRequest : class
        {
            var context = RequestContext(request);
            var resource = SessionStoreSecurityBinding.Resource(Store.Descriptor.Key, context.ToAddress());
            var grant = new SecurityGrant(
                new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), context.Authorization.Scope,
                context.Identity, context.Authorization, Store.SecurityAudience, kind, effect, [resource],
                RequestFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
                _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow().AddDays(1), 1);
            await _grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
            return new AuthorizedSessionStoreRequest<TRequest>(
                request, Store.Descriptor.Key, grant,
                new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), null));
        }

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());

        public ValueTask DisposeAsync()
        {
            _services.Dispose();
            return ValueTask.CompletedTask;
        }

        public static SessionOperationContext SessionContext(SessionAddress address, int offset)
        {
            var identity = Identity();
            var correlation = Correlation(offset);
            return new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));
        }

        public static MessageSessionEntry MessageEntry(SessionDescriptor descriptor, int offset, long sequence, string text)
        {
            var correlation = Correlation(offset);
            var message = new UserMessage(
                Identifier<MessageId>(offset + 1), descriptor.Address.AgentId,
                descriptor.Address.SessionId, descriptor.ConversationId, descriptor.ActiveBranchId,
                correlation.RunId, null, Timestamp(offset), MessageState.Complete,
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
            return new MessageSessionEntry(
                Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation,
                descriptor.ActiveBranchId, new SessionSequence(sequence), null, Timestamp(offset),
                new SchemaVersion("1"), message);
        }

        public static MessageSessionEntry AssistantToolCallEntry(SessionDescriptor descriptor, int offset, long sequence)
        {
            var correlation = Correlation(offset);
            using var arguments = JsonDocument.Parse("""{"path":"README.md","limit":10}""");
            var toolCall = new ToolCallPart(
                Identifier<ToolCallId>(offset + 5),
                new ToolReference(new ToolAlias("read"), null, null),
                arguments.RootElement.Clone(),
                new ProviderToolCallId("call_1"),
                ExtensionData.Empty);
            var message = new AssistantMessage(
                Identifier<MessageId>(offset + 1), descriptor.Address.AgentId,
                descriptor.Address.SessionId, descriptor.ConversationId, descriptor.ActiveBranchId,
                correlation.RunId, null, Timestamp(offset), MessageState.Complete,
                [toolCall],
                new AssistantResponseMetadata(
                    Identifier<ModelRequestId>(offset + 6),
                    new ProviderResponseIdentity(
                        new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("m"), new ModelId("m"), null, null, null),
                    NormalizedStopReason.ToolUse, null, ModelUsage.NotReported, ExtensionData.Empty),
                ExtensionData.Empty);
            return new MessageSessionEntry(
                Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation,
                descriptor.ActiveBranchId, new SessionSequence(sequence), null, Timestamp(offset),
                new SchemaVersion("1"), message);
        }

        private static SessionStoreCreateRequest CreateStoreRequest()
        {
            var agentId = Identifier<AgentId>(1);
            var identity = Identity();
            var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
            var authorization = Authorization(agentId, null, correlation, identity);
            var logical = new SessionCreateRequest(
                agentId, identity, authorization, Identifier<ConversationId>(3), new IdempotencyKey("create"), ExtensionData.Empty);
            var address = new SessionAddress(agentId, Identifier<SessionId>(4));
            var context = new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));
            return new SessionStoreCreateRequest(logical, address, context);
        }

        private static SecurityAuthorizationContext Authorization(
            AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
            new(new SecurityProfileKey("conformance"), new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:conformance-policy")),
                new ComponentKey<ISecurityAuthority>("conformance"), new AgentDefinitionRevision(1),
                new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

        private static ExecutionIdentity Identity() =>
            TestExecutionIdentity.Create(new TenantId("tenant-owner"), new PrincipalId("owner"), ExecutionSubjectKind.Human);

        private static InRunOperationCorrelation Correlation(int offset) =>
            new(Identifier<OperationId>(offset), Identifier<RunId>(offset + 1), null);

        private static DateTimeOffset Timestamp(int offset) => DateTimeOffset.UnixEpoch.AddSeconds(offset + 1);

        private static T Identifier<T>(int value)
        {
            var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
            return typeof(T) switch
            {
                var t when t == typeof(AgentId) => (T) (object) new AgentId(guid),
                var t when t == typeof(SessionId) => (T) (object) new SessionId(guid),
                var t when t == typeof(ConversationId) => (T) (object) new ConversationId(guid),
                var t when t == typeof(OperationId) => (T) (object) new OperationId(guid),
                var t when t == typeof(RunId) => (T) (object) new RunId(guid),
                var t when t == typeof(MessageId) => (T) (object) new MessageId(guid),
                var t when t == typeof(SessionEntryId) => (T) (object) new SessionEntryId(guid),
                var t when t == typeof(ToolCallId) => (T) (object) new ToolCallId(guid),
                var t when t == typeof(ModelRequestId) => (T) (object) new ModelRequestId(guid),
                var t when t == typeof(SecurityPolicySnapshotId) => (T) (object) new SecurityPolicySnapshotId(guid),
                _ => throw new NotSupportedException(typeof(T).Name),
            };
        }

        private Guid NextGuid()
        {
            var value = Interlocked.Increment(ref _nextIdentity);
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, value);
            bytes[15] = 7;
            return new Guid(bytes);
        }

        private static SessionOperationContext RequestContext<TRequest>(TRequest request)
            where TRequest : class => request switch
            {
                SessionStoreCreateRequest value => value.Context,
                SessionOperationContext value => value,
                SessionAppendRequest value => value.Context,
                SessionReadRequest value => value.Context,
                _ => throw new InvalidOperationException($"Unsupported request {typeof(TRequest).FullName}."),
            };

        private static InputFingerprint RequestFingerprint<TRequest>(TRequest request)
            where TRequest : class => request switch
            {
                SessionStoreCreateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
                SessionOperationContext value => SessionStoreSecurityBinding.Fingerprint(value),
                SessionAppendRequest value => SessionStoreSecurityBinding.Fingerprint(value),
                SessionReadRequest value => SessionStoreSecurityBinding.Fingerprint(value),
                _ => throw new InvalidOperationException($"Unsupported request {typeof(TRequest).FullName}."),
            };
    }
}
