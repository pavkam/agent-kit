// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using System.Text.Json;

using AgentKit.Conformance;
using AgentKit.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

    // ---- ProvisionLaneAsync edge cases the shared conformance suite does not exercise. ----

    [Fact]
    public async Task ProvisionLaneAsync_WhenSessionUnavailable_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var bogusAddress = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var laneId = Coverage.Identifier<ExecutionLaneId>(1000);
        var context = Coverage.LaneContext(
            bogusAddress, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1001), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(new BranchId(Guid.NewGuid()), null), new SessionVersion(0),
            new SessionEntryId(Guid.NewGuid()), 1000, "missing-session");

        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>().SafeMessage.ShouldBe("The session is unavailable.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenReplayedWithSameEvidence_ReturnsExistingProvision()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-replay-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1010);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1011), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1012), 1010, "lane-replay");

        var first = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var replay = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        var replayed = replay.ShouldBeOfType<SessionExecutionLaneProvisioned>();
        replayed.Existing.ShouldBeTrue();
        replayed.ExecutionLaneId.ShouldBe(first.ExecutionLaneId);
        replayed.SessionVersion.ShouldBe(first.SessionVersion);
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenReplayedWithDifferentEvidence_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-conflict-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1020);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1021), null));
        var first = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1022), 1020, "lane-key-reused");
        var second = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1023), 1021, "lane-key-reused");

        _ = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, first, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, second, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage
            .ShouldBe("The provisioning idempotency key was reused with different evidence.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenLaneAlreadyProvisioned_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-dup-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1030);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1031), null));
        var first = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1032), 1030, "lane-dup-first");
        var firstResult = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, first, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var second = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), firstResult.SessionVersion,
            Coverage.Identifier<SessionEntryId>(1033), 1031, "lane-dup-second");

        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, second, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage
            .ShouldBe("The execution lane is already provisioned.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenExpectedVersionIsStale_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-stale-version-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1040);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1041), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), new SessionVersion(descriptor.Version.Value + 41),
            Coverage.Identifier<SessionEntryId>(1042), 1040, "lane-stale-version");

        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage
            .ShouldBe("The expected session version is stale.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenBranchCursorIsStale_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-stale-cursor-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1050);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1051), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, Coverage.Identifier<SessionEntryId>(1052)), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1053), 1050, "lane-stale-cursor");

        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage
            .ShouldBe("The branch cursor is stale or unavailable.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenBranchAlreadyOwnedByAnotherLane_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-owned-create");
        var firstLaneId = Coverage.Identifier<ExecutionLaneId>(1060);
        var firstContext = Coverage.LaneContext(
            descriptor.Address, firstLaneId, Coverage.Identity(),
            new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1061), null));
        var first = Coverage.ProvisionRequest(
            context: firstContext, branchCursor: new SessionBranchCursor(descriptor.ActiveBranchId, null),
            expectedVersion: descriptor.Version, entryId: Coverage.Identifier<SessionEntryId>(1062), offset: 1060,
            idempotencyKey: "lane-owned-first");
        var firstResult = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, first, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var secondLaneId = Coverage.Identifier<ExecutionLaneId>(1063);
        var secondContext = Coverage.LaneContext(
            descriptor.Address, secondLaneId, Coverage.Identity(),
            new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1064), null));
        // The first lane's provisioning already advanced the branch tip; the second request must name that real
        // cursor to pass the staleness check before reaching the "already owned" check it exercises.
        var second = Coverage.ProvisionRequest(
            context: secondContext, branchCursor: firstResult.BranchCursor,
            expectedVersion: firstResult.SessionVersion, entryId: Coverage.Identifier<SessionEntryId>(1065), offset: 1063,
            idempotencyKey: "lane-owned-second");

        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, second, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage
            .ShouldBe("The branch is already owned by another execution lane.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenEntryIdIsReserved_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-entry-reserved-create");
        var context = Coverage.SessionContext(descriptor.Address, 1070);
        var reservedEntry = Coverage.MessageEntry(descriptor, 1071, 1, "reserved");
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("lane-entry-reserved-seed"), [reservedEntry]);
        var appended = (SessionAppended) await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var laneId = Coverage.Identifier<ExecutionLaneId>(1072);
        var laneContext = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1073), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, reservedEntry.Id), appended.NewVersion,
            reservedEntry.Id, Coverage.Profile(), Coverage.Configuration(), Coverage.Timestamp(1074), new IdempotencyKey("lane-entry-reserved"));

        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage
            .ShouldBe("The provisioning entry identity is already reserved.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenProvisioningEntryHasNoCodec_ReturnsRejectedWithoutMutation()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture(
            new RejectingCodecCatalog(typeof(ExecutionLaneProvisionedSessionEntry)));
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "lane-no-codec-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1080);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1081), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1082), 1080, "lane-no-codec");

        var result = await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(
            await Coverage.AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>().SafeMessage
            .ShouldBe("Entry at position 0 has no durable codec in the selected session store: Forced rejection for coverage test.");
        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(descriptor.Version);
    }

    // ---- AdmitInputAsync edge cases the shared conformance suite does not exercise. ----

    [Fact]
    public async Task AdmitInputAsync_WhenSessionUnavailable_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var bogusAddress = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var laneId = Coverage.Identifier<ExecutionLaneId>(1100);
        var context = Coverage.LaneContext(
            bogusAddress, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1101), null));
        var admission = Coverage.AdmissionRequest(
            context, Coverage.Identifier<AdmissionId>(1102), Coverage.Identifier<InputId>(1103),
            Coverage.Identifier<SessionEntryId>(1104), new SessionVersion(0), new SessionLaneRevision(1),
            new SessionBranchCursor(new BranchId(Guid.NewGuid()), null), "missing-session");

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.AddressNotFound);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenReplayedWithSameFingerprint_ReturnsAcceptedReceipt()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1110, "admit-replay");

        var replay = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, prepared.Admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        // The exact same admission request (same idempotency key) replays the original receipt by content fingerprint.
        replay.ShouldBeOfType<AcceptedInput>().Receipt.Existing.ShouldBeTrue();
    }

    [Fact]
    public async Task AdmitInputAsync_WhenReplayedWithDifferentEvidence_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1120, "admit-key-reused");
        var second = Coverage.AdmissionRequest(
            prepared.Context, Coverage.Identifier<AdmissionId>(1126), Coverage.Identifier<InputId>(1127),
            Coverage.Identifier<SessionEntryId>(1128), prepared.Provisioned.SessionVersion, prepared.Provisioned.LaneRevision,
            prepared.Provisioned.BranchCursor, "admit-key-reused-two");
        // Same idempotency key as `prepared.Admission` ("admit-key-reused-admit"), but different admission/input/entry identities.
        var replayed = new SessionInputAdmissionRequest(
            prepared.Context, second.AdmissionId, second.EntryId, second.OriginalPayload, second.EffectivePayload,
            second.Preprocessing, second.AdmittedAt, second.ExpectedVersion, second.ExpectedLaneRevision, second.BranchCursor,
            prepared.Admission.IdempotencyKey, second.MaximumPendingInputs);

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, replayed, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason
            .ShouldBe("The admission idempotency key was reused with different evidence.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenSameOriginalInputAdmittedTwiceWithDifferentKeyAndSameEvidence_ReturnsSameReceipt()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1130, "admit-same-input");
        // Reuses `prepared.Admission.OriginalPayload.Id` (the caller input identity) under a different idempotency key
        // but with otherwise-equivalent evidence, so `GetAdmissionByInputIdAsync` finds the canonical admission.
        var repeated = new SessionInputAdmissionRequest(
            prepared.Context, Coverage.Identifier<AdmissionId>(1133), Coverage.Identifier<SessionEntryId>(1134),
            prepared.Admission.OriginalPayload, prepared.Admission.EffectivePayload, prepared.Admission.Preprocessing,
            prepared.Admission.AdmittedAt, prepared.Provisioned.SessionVersion, prepared.Provisioned.LaneRevision,
            prepared.Provisioned.BranchCursor, new IdempotencyKey("admit-same-input-retry"), prepared.Admission.MaximumPendingInputs);

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, repeated, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<AcceptedInput>().Receipt.AdmittedSequence.ShouldBe(prepared.Accepted.Receipt.AdmittedSequence);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenExpectedVersionIsStale_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "admit-stale-version-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1140);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1141), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1142), 1140, "admit-stale-version-provision");
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var admission = Coverage.AdmissionRequest(
            context, Coverage.Identifier<AdmissionId>(1143), Coverage.Identifier<InputId>(1144),
            Coverage.Identifier<SessionEntryId>(1145), new SessionVersion(provisioned.SessionVersion.Value + 41),
            provisioned.LaneRevision, provisioned.BranchCursor, "admit-stale-version");

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.StaleVersion);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenLaneIsNotProvisioned_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "admit-no-lane-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1150);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1151), null));
        var admission = Coverage.AdmissionRequest(
            context, Coverage.Identifier<AdmissionId>(1152), Coverage.Identifier<InputId>(1153),
            Coverage.Identifier<SessionEntryId>(1154), descriptor.Version, new SessionLaneRevision(1),
            new SessionBranchCursor(descriptor.ActiveBranchId, null), "admit-no-lane");

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.AddressNotFound);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenLaneRevisionOrCursorIsStale_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "admit-stale-lane-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1160);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1161), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1162), 1160, "admit-stale-lane-provision");
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var admission = Coverage.AdmissionRequest(
            context, Coverage.Identifier<AdmissionId>(1163), Coverage.Identifier<InputId>(1164),
            Coverage.Identifier<SessionEntryId>(1165), provisioned.SessionVersion, new SessionLaneRevision(provisioned.LaneRevision.Value + 7),
            provisioned.BranchCursor, "admit-stale-lane");

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.StaleVersion);
    }

    [Fact]
    public async Task AdmitInputAsync_WhenEntryIdIsReserved_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1170, "admit-entry-reserved");
        // The prior admission already advanced session version, lane revision, and cursor; compute the current state.
        var expectedVersion = new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1);
        var expectedLaneRevision = new SessionLaneRevision(prepared.Provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, prepared.Admission.EntryId);
        // Reuses the already-committed provisioning entry identity (offset 1170 + 2) as the new admission's reserved entry id.
        var admission = new SessionInputAdmissionRequest(
            prepared.Context, Coverage.Identifier<AdmissionId>(1176), Coverage.Identifier<SessionEntryId>(1172),
            new AgentInput(Coverage.Identifier<InputId>(1177), InputDelivery.FollowUp,
                [new TextPart("reserved-entry", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty),
            new AgentInput(Coverage.Identifier<InputId>(1177), InputDelivery.FollowUp,
                [new TextPart("reserved-entry", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty),
            new InputPreprocessingManifest(
                new ConfigurationVersion(1), new InputFingerprint("sha256:reserved:original"), new InputFingerprint("sha256:reserved:effective")),
            Coverage.Timestamp(1178), expectedVersion, expectedLaneRevision, currentCursor, new IdempotencyKey("admit-entry-reserved"), 8);

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason.ShouldBe("The admission entry identity is already reserved.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenPendingCapacityIsExceeded_ReturnsQueueCapacityExceeded()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1180, "admit-capacity");
        // The prior admission committed one pending input; a bound of one is already met.
        var expectedVersion = new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1);
        var expectedLaneRevision = new SessionLaneRevision(prepared.Provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, prepared.Admission.EntryId);
        var second = Coverage.AdmissionRequest(
            prepared.Context, Coverage.Identifier<AdmissionId>(1186), Coverage.Identifier<InputId>(1187),
            Coverage.Identifier<SessionEntryId>(1188), expectedVersion, expectedLaneRevision, currentCursor,
            "admit-capacity-second", maximumPendingInputs: 1);

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, second, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<QueueCapacityExceeded>();
    }

    [Fact]
    public async Task AdmitInputAsync_WhenStoredAdmissionEvidenceDiffers_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1190, "admit-evidence-diff");
        // Reuses the already-admitted original input identity, but with structurally different canonical content.
        var conflicting = new SessionInputAdmissionRequest(
            prepared.Context, Coverage.Identifier<AdmissionId>(1196), Coverage.Identifier<SessionEntryId>(1197),
            new AgentInput(prepared.Admission.OriginalPayload.Id, InputDelivery.FollowUp,
                [new TextPart("different-text", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty),
            new AgentInput(prepared.Admission.OriginalPayload.Id, InputDelivery.FollowUp,
                [new TextPart("different-text", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty),
            prepared.Admission.Preprocessing, Coverage.Timestamp(1198), prepared.Provisioned.SessionVersion,
            prepared.Provisioned.LaneRevision, prepared.Provisioned.BranchCursor, new IdempotencyKey("admit-evidence-diff-retry"), 8);

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, conflicting, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason
            .ShouldBe("The input identity was already admitted with different immutable evidence.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenEntryHasNoCodec_ReturnsRejectedWithoutMutation()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture(new RejectingCodecCatalog(typeof(InputAdmittedSessionEntry)));
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "admit-no-codec-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1600);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1601), null));
        var provision = Coverage.ProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Coverage.Identifier<SessionEntryId>(1602), 1600, "admit-no-codec-provision");
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await Coverage.AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var admission = Coverage.AdmissionRequest(
            context, Coverage.Identifier<AdmissionId>(1603), Coverage.Identifier<InputId>(1604),
            Coverage.Identifier<SessionEntryId>(1605), provisioned.SessionVersion, provisioned.LaneRevision,
            provisioned.BranchCursor, "admit-no-codec");

        var result = await store.AdmitInputAsync(
            await Coverage.AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.InvalidInput);
    }

    // ---- AcceptRunAsync edge cases the shared conformance suite does not exercise. ----

    [Fact]
    public async Task AcceptRunAsync_WhenSessionUnavailable_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var bogusAddress = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var laneId = Coverage.Identifier<ExecutionLaneId>(1200);
        var context = Coverage.LaneContext(
            bogusAddress, laneId, Coverage.Identity(), new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1201), null));
        var runId = Coverage.Identifier<RunId>(1202);
        var turnId = Coverage.Identifier<TurnId>(1203);
        var start = new SessionRunStartRequest(
            context, Coverage.Identifier<AdmissionId>(1204), [Coverage.Identifier<AdmissionId>(1204)], new SessionSequence(1),
            new SessionLaneRevision(1), new SessionVersion(0), new SessionBranchCursor(new BranchId(Guid.NewGuid()), null),
            null, runId, turnId, Coverage.Identifier<SessionEntryId>(1205), [Coverage.Identifier<SessionEntryId>(1206)],
            [Coverage.Identifier<MessageId>(1207)], Coverage.Identifier<SessionEntryId>(1208), new OperationStateRevision(1),
            Coverage.Profile(), Coverage.Configuration(),
            Coverage.Authorization(bogusAddress.AgentId, bogusAddress.SessionId,
                new InRunOperationCorrelation(context.Correlation.OperationId, runId, turnId), context.Identity),
            Coverage.Timestamp(1209), new IdempotencyKey("missing-session"));

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartRejected>().SafeReason.ShouldBe("The session is unavailable.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenLaneDoesNotExist_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1210, "accept-no-lane");
        var otherLaneId = Coverage.Identifier<ExecutionLaneId>(1219);
        var otherLaneContext = Coverage.LaneContext(
            prepared.Descriptor.Address, otherLaneId, Coverage.Identity(), prepared.Context.Correlation);
        var start = Coverage.StartRequest(prepared, 1220, context: otherLaneContext);

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.LaneRevision);
        conflict.SafeReason.ShouldBe("The selected lane does not exist.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenLaneRevisionIsStale_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1230, "accept-stale-lane-revision");
        var start = Coverage.StartRequest(
            prepared, 1240, expectedLaneRevision: new SessionLaneRevision(prepared.Provisioned.LaneRevision.Value + 41));

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.LaneRevision);
        conflict.SafeReason.ShouldBe("The selected lane revision is stale.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenBranchCursorIsStale_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1250, "accept-stale-cursor");
        var start = Coverage.StartRequest(
            prepared, 1260, branchCursor: new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, null));

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.BranchCursor);
        conflict.SafeReason.ShouldBe("The selected branch cursor is stale.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenExpectedVersionIsStale_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1270, "accept-stale-version");
        var start = Coverage.StartRequest(
            prepared, 1280, expectedVersion: new SessionVersion(prepared.Provisioned.SessionVersion.Value + 41));

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.SessionVersion);
        conflict.SafeReason.ShouldBe("The expected session version is stale.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenAdmissionIdentityDiffersFromContext_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1290, "accept-identity-diff");
        var impersonator = Coverage.Identity("tenant-owner", "impersonator");
        var mismatchedContext = new SessionOperationContext(
            prepared.Context.AgentId, prepared.Context.SessionId, prepared.Context.ExecutionLaneId, prepared.Context.Correlation,
            impersonator,
            Coverage.Authorization(prepared.Context.AgentId, prepared.Context.SessionId, prepared.Context.Correlation, impersonator));
        var start = Coverage.StartRequest(prepared, 1300, context: mismatchedContext);

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.AdmissionIdentity);
        conflict.SafeReason.ShouldBe("Every promoted admission must retain the exact authorized run identity.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenPromotionPlanIsNoLongerEligible_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1310, "accept-plan-ineligible");
        var bogusAdmissionId = Coverage.Identifier<AdmissionId>(1319);
        var start = Coverage.StartRequest(
            prepared, 1320, selectedAdmissionIds: [bogusAdmissionId], initiatingAdmissionId: bogusAdmissionId);

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.PromotionPlan);
        conflict.SafeReason.ShouldBe("The exact promotion plan is no longer eligible.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenInitiatingAdmissionCorrelationDiffersFromContext_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1330, "accept-correlation-diff");
        // A different before-run context (same lane and identity, different operation) so the initiating admission's
        // retained correlation no longer matches this start request's own context correlation.
        var differentContext = Coverage.LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1339), null));
        var start = Coverage.StartRequest(prepared, 1340, context: differentContext);

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.AdmissionCorrelation);
        conflict.SafeReason.ShouldBe("The initiating admission correlation differs from the proposed run.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenReservedEntryIdCollides_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1350, "accept-entry-collides");
        // Reuses the already-committed admission entry identity as one of the promoted history-entry IDs.
        var start = Coverage.StartRequest(prepared, 1360, entryIds: [prepared.Admission.EntryId]);

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.PromotionPlan);
        conflict.SafeReason.ShouldBe("A reserved session-entry identity is already in use.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenReservedMessageIdCollides_ReturnsConflict()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1370, "accept-message-collides");
        // The provisioning and admission entries already committed sequences 1 and 2; the seeded message is sequence 3.
        var seededMessage = Coverage.MessageEntry(prepared.Descriptor, 1380, 3, "already-committed");
        var seedAppend = new SessionAppendRequest(
            prepared.Context, prepared.Descriptor.ActiveBranchId,
            new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1), new IdempotencyKey("accept-message-collides-seed"),
            [seededMessage]);
        _ = (await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, seedAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        // Reuses the already-committed message identity as the promoted run's own message ID.
        var start = Coverage.StartRequest(
            prepared, 1390, expectedVersion: new SessionVersion(prepared.Provisioned.SessionVersion.Value + 2),
            branchCursor: new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, seededMessage.Id),
            messageIds: [seededMessage.Message.Id]);

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionRunStartConflict>();
        conflict.Kind.ShouldBe(SessionRunStartConflictKind.PromotionPlan);
        conflict.SafeReason.ShouldBe("A reserved message identity is already in use.");
    }

    // ---- ReleaseRunAsync and small session-not-found edge cases. ----

    [Fact]
    public async Task ReleaseRunAsync_WhenSessionUnavailable_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var bogusAddress = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var laneId = Coverage.Identifier<ExecutionLaneId>(1400);
        var context = Coverage.LaneContext(
            bogusAddress, laneId, Coverage.Identity(),
            new InRunOperationCorrelation(Coverage.Identifier<OperationId>(1401), Coverage.Identifier<RunId>(1402), null));
        var release = Coverage.ReleaseRequest(context, new OperationStateRevision(1), new SessionVersion(0), "missing-session-release");

        var result = await store.ReleaseRunAsync(
            await Coverage.AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<SessionRunReleaseRejected>();
        rejected.Kind.ShouldBe(SessionRunReleaseRejectionKind.LaneNotFound);
        rejected.SafeReason.ShouldBe("The session is unavailable.");
    }

    [Fact]
    public async Task ReleaseRunAsync_WhenLaneDoesNotExist_ReturnsRejected()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "release-no-lane-create");
        var laneId = Coverage.Identifier<ExecutionLaneId>(1410);
        var context = Coverage.LaneContext(
            descriptor.Address, laneId, Coverage.Identity(),
            new InRunOperationCorrelation(Coverage.Identifier<OperationId>(1411), Coverage.Identifier<RunId>(1412), null));
        var release = Coverage.ReleaseRequest(context, new OperationStateRevision(1), descriptor.Version, "release-no-lane");

        var result = await store.ReleaseRunAsync(
            await Coverage.AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<SessionRunReleaseRejected>();
        rejected.Kind.ShouldBe(SessionRunReleaseRejectionKind.LaneNotFound);
        rejected.SafeReason.ShouldBe("The selected lane does not exist.");
    }

    [Fact]
    public async Task ReadAsync_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var context = Coverage.SessionContext(address, 1420);
        var read = new SessionReadRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), 10);

        var result = await store.ReadAsync(
            await Coverage.AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionReadNotFound(address));
    }

    [Fact]
    public async Task LookupInputAsync_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var context = Coverage.LaneContext(
            address, Coverage.Identifier<ExecutionLaneId>(1430), Coverage.Identity(),
            new BeforeRunOperationCorrelation(Coverage.Identifier<OperationId>(1431), null));
        var input = new AgentInput(
            Coverage.Identifier<InputId>(1432), InputDelivery.FollowUp,
            [new TextPart("lookup", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var lookup = new SessionInputLookupRequest(context, input, new InputFingerprint("sha256:lookup:missing"));

        var result = await store.LookupInputAsync(
            await Coverage.AuthorizeAsync(fixture, lookup, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionInputNotFound>();
    }

    [Fact]
    public async Task LoadRunStateAsync_WhenSessionDoesNotExist_ReturnsUnavailable()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Coverage.Identifier<AgentId>(1), Coverage.Identifier<SessionId>(4));
        var context = Coverage.LaneContext(
            address, Coverage.Identifier<ExecutionLaneId>(1440), Coverage.Identity(),
            new InRunOperationCorrelation(Coverage.Identifier<OperationId>(1441), Coverage.Identifier<RunId>(1442), null));
        var load = new SessionRunStateRequest(context);

        var result = await store.LoadRunStateAsync(
            await Coverage.AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStateUnavailable>().SafeReason.ShouldBe("The requested operation state is unavailable.");
    }

    // ---- AppendAsync edge cases the shared conformance suite does not exercise. ----

    [Fact]
    public async Task AppendAsync_WhenBranchDoesNotExist_ReturnsNotFound()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "append-no-branch-create");
        var context = Coverage.SessionContext(descriptor.Address, 1450);
        var unknownBranch = new BranchId(Guid.NewGuid());
        var append = new SessionAppendRequest(
            context, unknownBranch, descriptor.Version, new IdempotencyKey("append-no-branch"),
            [Coverage.MessageEntry(descriptor, 1451, 1, "orphan", unknownBranch)]);

        var result = await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionAppendNotFound(descriptor.Address));
    }

    [Fact]
    public async Task AppendAsync_WhenEntryIdIsAlreadyReserved_ReturnsTypedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "append-entry-reused-create");
        var context = Coverage.SessionContext(descriptor.Address, 1460);
        var first = Coverage.MessageEntry(descriptor, 1461, 1, "first");
        var firstAppend = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("append-entry-reused-seed"), [first]);
        var firstResult = (SessionAppended) await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, firstAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        // Reuses the already-committed entry identity for a structurally distinct second entry.
        var reused = new MessageSessionEntry(
            first.Id, descriptor.Address, first.Correlation, descriptor.ActiveBranchId, new SessionSequence(2), first.Id,
            first.RecordedAt, first.SchemaVersion, first.Message);
        var secondAppend = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, firstResult.NewVersion, new IdempotencyKey("append-entry-reused"), [reused]);

        var result = await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, secondAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldBe("An appended entry identity is already reserved.");
    }

    [Fact]
    public async Task AppendAsync_WhenMessageIdIsAlreadyReserved_ReturnsTypedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "append-message-reused-create");
        var context = Coverage.SessionContext(descriptor.Address, 1470);
        var first = Coverage.MessageEntry(descriptor, 1471, 1, "first");
        var firstAppend = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("append-message-reused-seed"), [first]);
        var firstResult = (SessionAppended) await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, firstAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        // A structurally distinct new entry that reuses the already-committed message identity.
        var reusedMessage = new UserMessage(
            first.Message.Id, descriptor.Address.AgentId, descriptor.Address.SessionId, descriptor.ConversationId,
            descriptor.ActiveBranchId, first.Message.RunId, null, Coverage.Timestamp(1472), MessageState.Complete,
            [new TextPart("second", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var reused = new MessageSessionEntry(
            Coverage.Identifier<SessionEntryId>(1479), descriptor.Address, Coverage.Correlation(1472), descriptor.ActiveBranchId,
            new SessionSequence(2), first.Id, Coverage.Timestamp(1472), new SchemaVersion("1"), reusedMessage);
        var secondAppend = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, firstResult.NewVersion, new IdempotencyKey("append-message-reused"), [reused]);

        var result = await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, secondAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldBe("An appended message identity is already reserved.");
    }

    // ---- EnforceAsync (protected-boundary) edge cases the shared conformance suite does not exercise. ----

    [Fact]
    public async Task LoadAsync_WhenGrantTargetsADifferentStore_ReturnsTypedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "enforce-wrong-store-create");
        var context = Coverage.SessionContext(descriptor.Address, 1480);
        var authorized = await Coverage.AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe);
        var mismatched = new AuthorizedSessionStoreRequest<SessionOperationContext>(
            authorized.Request, new SessionStoreKey("some-other-store"), authorized.Grant, authorized.Intent);

        var result = await store.LoadAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("The grant targets a different session store.");
    }

    [Fact]
    public async Task LoadAsync_WhenEnforcementIntentFenceDiffersFromRequest_ReturnsTypedFailure()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "enforce-fence-diff-create");
        var context = Coverage.SessionContext(descriptor.Address, 1490);
        var authorized = await Coverage.AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe);
        // Only SessionRunStartRequest ever carries a fence; any nonnull intent fence for another request kind mismatches.
        var mismatched = new AuthorizedSessionStoreRequest<SessionOperationContext>(
            authorized.Request, authorized.StoreKey, authorized.Grant,
            new SecurityEnforcementIntent(authorized.Intent.Id, new FencingToken(1)));

        var result = await store.LoadAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("The enforcement intent fence differs from the session request.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenFencingTokenIsRequested_ReturnsRejectedForUnsupportedDistributedFencing()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1500, "accept-fenced");
        var start = Coverage.StartRequest(prepared, 1510, expectedFencingToken: new FencingToken(1));

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartRejected>().SafeReason
            .ShouldBe("The selected session store does not support distributed fencing.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenAcceptedEntryHasNoCodec_ReturnsRejectedWithoutMutation()
    {
        await using var fixture = new SqliteSessionStoreConformanceFixture(new RejectingCodecCatalog(typeof(OperationAcceptedSessionEntry)));
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await Coverage.ProvisionAndAdmitAsync(fixture, store, 1610, "accept-no-codec");
        var start = Coverage.StartRequest(prepared, 1620);

        var result = await store.AcceptRunAsync(
            await Coverage.AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(
            await Coverage.AuthorizeAsync(fixture, prepared.Context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartRejected>().SafeReason.ShouldContain("Forced rejection for coverage test.");
        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version
            .ShouldBe(new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1));
    }

    // ---- Persisted-payload decode failures and post-preflight encode inconsistency. ----

    [Fact]
    public async Task ReadAsync_WhenPersistedEntryDecodesToRejected_ThrowsJsonException()
    {
        // A committed entry that later becomes unreadable (for example after a codec is retired) must fail the
        // read as a typed exception rather than silently dropping or misinterpreting the row.
        var rejectedTypeId = new SessionEntryTypeId("agentkit.session/message");
        await using var fixture = new SqliteSessionStoreConformanceFixture(
            new DecodeRejectingCodecCatalog(rejectedTypeId, new SessionEntryDecodeRejected("Simulated malformed payload.")));
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "read-decode-rejected-create");
        var context = Coverage.SessionContext(descriptor.Address, 1700);
        var entry = Coverage.MessageEntry(descriptor, 1701, 1, "will-not-decode");
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("read-decode-rejected"), [entry]);
        _ = (await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        var read = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10);

        var exception = await Should.ThrowAsync<JsonException>(async () =>
            await store.ReadAsync(
                await Coverage.AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Simulated malformed payload.");
    }

    [Fact]
    public async Task ReadAsync_WhenPersistedEntryHasNoAvailableCodec_ThrowsJsonException()
    {
        var rejectedTypeId = new SessionEntryTypeId("agentkit.session/message");
        await using var fixture = new SqliteSessionStoreConformanceFixture(
            new DecodeRejectingCodecCatalog(
                rejectedTypeId,
                new SessionEntryOpaque(new SessionEntryWireEnvelope(rejectedTypeId, new SchemaVersion("1"), [0]))));
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "read-decode-opaque-create");
        var context = Coverage.SessionContext(descriptor.Address, 1710);
        var entry = Coverage.MessageEntry(descriptor, 1711, 1, "will-be-opaque");
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("read-decode-opaque"), [entry]);
        _ = (await store.AppendAsync(
            await Coverage.AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        var read = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10);

        var exception = await Should.ThrowAsync<JsonException>(async () =>
            await store.ReadAsync(
                await Coverage.AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("no available codec");
    }

    [Fact]
    public async Task AppendAsync_WhenCodecRejectsEntryAfterPreflightSucceeded_ThrowsInvalidOperationException()
    {
        // A defensive check: InsertEntryAsync re-encodes the already-preflighted entry and must fail loudly,
        // rather than silently persisting a different payload, if a codec somehow disagrees with its own preflight.
        await using var fixture = new SqliteSessionStoreConformanceFixture(new FlakyEncodeCodecCatalog(typeof(MessageSessionEntry)));
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await Coverage.CreateSessionAsync(fixture, store, "append-flaky-codec-create");
        var context = Coverage.SessionContext(descriptor.Address, 1720);
        var entry = Coverage.MessageEntry(descriptor, 1721, 1, "flaky");
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("append-flaky-codec"), [entry]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.AppendAsync(
                await Coverage.AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The codec catalog rejected an entry that the caller's preflight already proved encodable.");
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

    /// <summary>Builds the same first-party codec set the store's own DI registration installs.</summary>
    private static SessionEntryCodecCatalog RealCodecCatalog() => new(
        [
            new ExecutionLaneProvisionedSessionEntryCodec(
                TimeProvider.System, NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance),
            new InputPromotedSessionEntryCodec(TimeProvider.System, NullLogger<InputPromotedSessionEntryCodec>.Instance),
            new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance),
            new MessageSessionEntryCodec(),
            new InputAdmittedSessionEntryCodec(),
            new CompactionSessionEntryCodec(),
        ],
        TimeProvider.System);

    /// <summary>
    /// Wraps the real first-party codec catalog but forces <see cref="Encode"/> to reject one exact entry runtime
    /// type, so a test can exercise a store operation's codec-preflight failure without an entry kind that has no
    /// codec at all.
    /// </summary>
    private sealed class RejectingCodecCatalog: ISessionEntryCodecCatalog
    {
        private readonly SessionEntryCodecCatalog _inner = RealCodecCatalog();
        private readonly Type _rejectedType;

        public RejectingCodecCatalog(Type rejectedType) => _rejectedType = rejectedType;

        public SessionEntryEncodeResult Encode(SessionEntry entry) =>
            entry.GetType() == _rejectedType
                ? new SessionEntryEncodeRejected("Forced rejection for coverage test.")
                : _inner.Encode(entry);

        public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire) => _inner.Decode(wire);
    }

    /// <summary>
    /// Wraps the real first-party codec catalog but forces <see cref="Decode"/> to report a chosen outcome for one
    /// exact wire type identity, so a test can exercise a persisted, already-committed entry that later becomes
    /// unreadable (for example after a codec is retired) without corrupting raw database bytes directly.
    /// </summary>
    private sealed class DecodeRejectingCodecCatalog: ISessionEntryCodecCatalog
    {
        private readonly SessionEntryCodecCatalog _inner = RealCodecCatalog();
        private readonly SessionEntryTypeId _rejectedTypeId;
        private readonly SessionEntryDecodeResult _forcedResult;

        public DecodeRejectingCodecCatalog(SessionEntryTypeId rejectedTypeId, SessionEntryDecodeResult forcedResult)
        {
            _rejectedTypeId = rejectedTypeId;
            _forcedResult = forcedResult;
        }

        public SessionEntryEncodeResult Encode(SessionEntry entry) => _inner.Encode(entry);

        public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire) =>
            wire.TypeId == _rejectedTypeId ? _forcedResult : _inner.Decode(wire);
    }

    /// <summary>
    /// Wraps the real first-party codec catalog but rejects the second and later <see cref="Encode"/> call for one
    /// exact entry runtime type, simulating a codec that behaves inconsistently between the store's preflight
    /// encode and <see cref="SqliteSessionUnitOfWork.InsertEntryAsync"/>'s own encode of the same already-validated
    /// entry.
    /// </summary>
    private sealed class FlakyEncodeCodecCatalog: ISessionEntryCodecCatalog
    {
        private readonly SessionEntryCodecCatalog _inner = RealCodecCatalog();
        private readonly Type _flakyType;
        private int _calls;

        public FlakyEncodeCodecCatalog(Type flakyType) => _flakyType = flakyType;

        public SessionEntryEncodeResult Encode(SessionEntry entry) =>
            entry.GetType() == _flakyType && Interlocked.Increment(ref _calls) > 1
                ? new SessionEntryEncodeRejected("Forced post-preflight rejection for coverage test.")
                : _inner.Encode(entry);

        public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire) => _inner.Decode(wire);
    }

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

        public static async ValueTask<PreparedLane> ProvisionAndAdmitAsync(
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
            return new PreparedLane(descriptor, context, provisioned, admission, accepted);
        }

        /// <summary>The composed state of one provisioned lane with one committed admission.</summary>
        public sealed record PreparedLane(
            SessionDescriptor Descriptor, SessionOperationContext Context, SessionExecutionLaneProvisioned Provisioned,
            SessionInputAdmissionRequest Admission, AcceptedInput Accepted);

        public static SessionRunStartRequest StartRequest(
            PreparedLane prepared,
            int offset,
            SessionOperationContext? context = null,
            SessionVersion? expectedVersion = null,
            SessionBranchCursor? branchCursor = null,
            SessionLaneRevision? expectedLaneRevision = null,
            ImmutableArray<AdmissionId>? selectedAdmissionIds = null,
            AdmissionId? initiatingAdmissionId = null,
            ImmutableArray<SessionEntryId>? entryIds = null,
            ImmutableArray<MessageId>? messageIds = null,
            FencingToken? expectedFencingToken = null,
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
                expectedFencingToken, runId, turnId, Identifier<SessionEntryId>(offset + 2),
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
