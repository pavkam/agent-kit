// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines reusable protected-store behavior for interchangeable <see cref="ISessionStore"/> implementations.</summary>
/// <typeparam name="TFixture">The fixture that composes one store and supplies exact fresh authority.</typeparam>
public abstract class SessionStoreConformanceTests<TFixture>
    where TFixture : ISessionStoreConformanceFixture
{
    /// <summary>Creates an isolated fixture for one conformance case.</summary>
    /// <returns>A new fixture with no retained session state or spent grants.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies changed protected bytes are denied before creation and do not consume the exact valid request's grant.</summary>
    [Fact]
    public async Task CreateAsync_WhenGrantDoesNotBindExactRequest_DeniesBeforeAccess()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateStoreRequest();
        var authorized = await fixture.AuthorizeAsync(
            request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
            TestContext.Current.CancellationToken);
        var changedLogical = new SessionCreateRequest(
            request.Request.AgentId, request.Request.Identity, request.Request.Authorization,
            request.Request.ConversationId, request.Request.IdempotencyKey,
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                "conformance.changed", new ExtensionValue([1]))));
        var changedRequest = new SessionStoreCreateRequest(changedLogical, request.Address, request.Context);
        var mismatched = new AuthorizedSessionStoreRequest<SessionStoreCreateRequest>(
            changedRequest, authorized.StoreKey, authorized.Grant, authorized.Intent);

        var denied = await store.CreateAsync(mismatched, TestContext.Current.CancellationToken);
        var accepted = await store.CreateAsync(authorized, TestContext.Current.CancellationToken);

        _ = denied.ShouldBeOfType<SessionCreateFailed>();
        ((SessionCreated) accepted).Descriptor.Address.ShouldBe(request.Address);
    }

    /// <summary>Verifies an exactly reconstructed creation retry returns the original session instead of allocating another.</summary>
    [Fact]
    public async Task CreateAsync_WhenExactRequestIsReplayed_ReturnsOriginalReceipt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateStoreRequest();

        var first = (SessionCreated) await store.CreateAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var rebuiltLogical = new SessionCreateRequest(
            request.Request.AgentId, request.Request.Identity, request.Request.Authorization,
            request.Request.ConversationId, request.Request.IdempotencyKey, request.Request.Extensions);
        var rebuiltContext = new SessionOperationContext(
            request.Context.AgentId, request.Context.SessionId, request.Context.ExecutionLaneId,
            request.Context.Correlation, request.Context.Identity, request.Context.Authorization);
        var rebuiltRequest = new SessionStoreCreateRequest(
            rebuiltLogical, new SessionAddress(request.Address.AgentId, request.Address.SessionId), rebuiltContext);
        var replay = (SessionCreated) await store.CreateAsync(
            await AuthorizeAsync(fixture, rebuiltRequest, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        replay.ShouldBe(first);
        replay.Descriptor.Address.ShouldBe(request.Address);
    }

    /// <summary>Verifies stale compare-and-swap rejection leaves the previously committed append as the only history.</summary>
    [Fact]
    public async Task AppendAsync_WhenExpectedVersionIsStale_DoesNotPartiallyMutateHistory()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(20));
        var firstEntry = MessageEntry(descriptor, 30, 1, "first");
        var firstRequest = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("append-first"), [firstEntry]);
        var first = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, firstRequest, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var staleEntry = MessageEntry(descriptor, 31, 2, "stale");
        var staleRequest = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("append-stale"), [staleEntry]);

        var conflict = await store.AppendAsync(
            await AuthorizeAsync(fixture, staleRequest, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);

        first.NewVersion.Value.ShouldBe(descriptor.Version.Value + 1);
        _ = conflict.ShouldBeOfType<SessionAppendConflict>();
        page.Entries.ShouldBe([firstEntry]);
    }

    /// <summary>Verifies a fork point must be a committed sequence of the named parent branch, even when a sibling branch has independently committed an entry at the identical numeric sequence.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenSequenceBelongsToAnotherBranch_ReturnsParentNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(120));
        var mainAppend = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("main-1"),
            [MessageEntry(descriptor, 121, 1, "main")]);
        var mainAppended = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, mainAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var emptyFork = new SessionBranchRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("fork-empty"));
        var forked = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, emptyFork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        // The forked branch is empty, so its own sequence coordinates start at 1, independent of the
        // main branch's own tip. This intentionally allocates the numeric sequence 1 on both branches
        // to prove branch-local sequences do not collide or leak into a sibling's fork validation.
        var sideAppend = new SessionAppendRequest(
            context, forked.NewBranchId, new SessionVersion(mainAppended.NewVersion.Value + 1),
            new IdempotencyKey("side-1"), [MessageEntry(descriptor, 123, 1, "side", forked.NewBranchId)]);
        var sideAppended = await store.AppendAsync(
            await AuthorizeAsync(fixture, sideAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var foreignFork = new SessionBranchRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(2), new IdempotencyKey("fork-foreign"));

        var result = await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, foreignFork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var loaded = (SessionLoaded) await store.LoadAsync(
            await AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = sideAppended.ShouldBeOfType<SessionAppended>();
        result.ShouldBe(new SessionBranchParentNotFound(descriptor.ActiveBranchId, new SessionSequence(2)));
        loaded.Descriptor.Version.ShouldBe(new SessionVersion(mainAppended.NewVersion.Value + 2));
    }

    /// <summary>
    /// Verifies an append to a branch succeeds using that branch's own tip plus one even after a sibling
    /// branch has independently committed more entries than this branch has ever seen. A store that instead
    /// allocated sequences per session would reject this branch's append with a stale-sequence failure.
    /// </summary>
    [Fact]
    public async Task AppendAsync_WhenSiblingBranchAdvanced_AcceptsBranchTipPlusOne()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(300));
        var fork = new SessionBranchRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("fork-empty"));
        var forked = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        // Advance the sibling (main) branch well past the forked branch's own (empty) tip.
        SessionVersion mainVersion = new(1);
        for (var index = 0; index < 3; index++)
        {
            var mainAppend = new SessionAppendRequest(
                context, descriptor.ActiveBranchId, mainVersion, new IdempotencyKey($"main-{index}"),
                [MessageEntry(descriptor, 301 + index, index + 1, $"main-{index}")]);
            var mainAppended = (SessionAppended) await store.AppendAsync(
                await AuthorizeAsync(fixture, mainAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken);
            mainVersion = mainAppended.NewVersion;
        }

        // The forked branch is still empty, so its correct next sequence is 1, not 4.
        var forkAppend = new SessionAppendRequest(
            context, forked.NewBranchId, mainVersion, new IdempotencyKey("fork-append"),
            [MessageEntry(descriptor, 310, 1, "fork-first", forked.NewBranchId)]);

        var result = await store.AppendAsync(
            await AuthorizeAsync(fixture, forkAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var branchRead = new SessionReadRequest(context, forked.NewBranchId, new SessionSequence(0), 10);
        var branchPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, branchRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppended>();
        branchPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1]);
    }

    /// <summary>
    /// Verifies two branches that share ancestry allocate their own independent sequence coordinates: appending
    /// to each branch a different number of times leaves each branch's own sequences contiguous from one,
    /// unaffected by how many entries its sibling has committed.
    /// </summary>
    [Fact]
    public async Task CreateBranchAsync_ThenAppendOnBothBranches_KeepsIndependentSequences()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 2);
        var fork = new SessionBranchRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(1), new IdempotencyKey("fork"));
        var forked = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        // Main branch already has entries [1, 2]; append a third entry, sequence 3.
        var mainAppend = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(3), new IdempotencyKey("main-3"),
            [MessageEntry(descriptor, 320, 3, "main-3")]);
        var mainAppended = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, mainAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        // The forked branch retained only entry [1]; its own next two appends are sequence 2 then 3,
        // independent of the main branch's own sequence 3 entry above.
        var forkAppendOne = new SessionAppendRequest(
            context, forked.NewBranchId, mainAppended.NewVersion, new IdempotencyKey("fork-2"),
            [MessageEntry(descriptor, 330, 2, "fork-2", forked.NewBranchId)]);
        var forkAppendedOne = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, forkAppendOne, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var forkAppendTwo = new SessionAppendRequest(
            context, forked.NewBranchId, forkAppendedOne.NewVersion, new IdempotencyKey("fork-3"),
            [MessageEntry(descriptor, 340, 3, "fork-3", forked.NewBranchId)]);
        var forkAppendedTwo = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, forkAppendTwo, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        var mainPage = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);
        var forkRead = new SessionReadRequest(context, forked.NewBranchId, new SessionSequence(0), 10);
        var forkPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, forkRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = forkAppendedTwo;
        mainPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2, 3]);
        forkPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2, 3]);
        mainPage.Entries[^1].Id.ShouldNotBe(forkPage.Entries[^1].Id);
    }

    /// <summary>
    /// Verifies a paged read of one branch reports that branch's own tip as its upper sequence, unaffected by
    /// how many additional entries a sibling branch has independently committed.
    /// </summary>
    [Fact]
    public async Task ReadAsync_AfterSiblingAppends_ReportsBranchLocalUpperSequence()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 1);
        var fork = new SessionBranchRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(1), new IdempotencyKey("fork"));
        var forked = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        // Advance only the main branch, well beyond the forked branch's own single entry.
        var mainVersion = new SessionVersion(2);
        for (var index = 0; index < 3; index++)
        {
            var mainAppend = new SessionAppendRequest(
                context, descriptor.ActiveBranchId, mainVersion, new IdempotencyKey($"main-{index}"),
                [MessageEntry(descriptor, 350 + index, index + 2, $"main-{index}")]);
            var mainAppended = (SessionAppended) await store.AppendAsync(
                await AuthorizeAsync(fixture, mainAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken);
            mainVersion = mainAppended.NewVersion;
        }

        var forkRead = new SessionReadRequest(context, forked.NewBranchId, new SessionSequence(0), 10);
        var forkPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, forkRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        forkPage.Snapshot.ShouldNotBeNull().UpperSequence.ShouldBe(new SessionSequence(1));
        forkPage.ThroughSequence.ShouldBe(new SessionSequence(1));
        forkPage.HasMore.ShouldBeFalse();
    }

    /// <summary>Verifies an append batch whose first sequence skips the branch's own tip is a typed failure, even when that sequence matches the whole session's total entry count.</summary>
    [Fact]
    public async Task AppendAsync_WhenSequenceSkipsBranchTip_ReturnsTypedFailure()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(360));
        var fork = new SessionBranchRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("fork-empty"));
        var forked = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var mainAppend = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(1), new IdempotencyKey("main-1"),
            [MessageEntry(descriptor, 361, 1, "main-1")]);
        var mainAppended = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, mainAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        // The forked branch is empty (tip 0), so its correct next sequence is 1. Presenting 2 here — the
        // whole session's total committed entry count, which a session-wide allocator would have produced —
        // must be rejected as a typed failure rather than silently accepted or misattributed as a conflict.
        var wrongAppend = new SessionAppendRequest(
            context, forked.NewBranchId, mainAppended.NewVersion, new IdempotencyKey("fork-skip"),
            [MessageEntry(descriptor, 362, 2, "fork-skip", forked.NewBranchId)]);

        var result = await store.AppendAsync(
            await AuthorizeAsync(fixture, wrongAppend, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendFailed>();
    }

    /// <summary>Verifies an appended entry whose self-declared branch differs from the request's target branch is a typed failure, not silently committed under the request's branch.</summary>
    [Fact]
    public async Task AppendAsync_WhenAnEntryDeclaresADifferentBranchThanTheRequest_ReturnsTypedFailure()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(370));
        var fork = new SessionBranchRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("fork-empty-370"));
        var forked = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        // The request targets the active branch, but the entry's self-declared BranchId names the sibling
        // forked branch instead. This must be rejected rather than committed under the request's branch while
        // the stored payload's own BranchId disagrees with where it actually lives.
        var mismatched = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(1), new IdempotencyKey("branch-mismatch"),
            [MessageEntry(descriptor, 371, 1, "mismatched-branch", forked.NewBranchId)]);

        var result = await store.AppendAsync(
            await AuthorizeAsync(fixture, mismatched, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendFailed>();
    }

    /// <summary>Verifies an appended entry whose self-declared address differs from the addressed session is a typed failure, not silently committed under the addressed session.</summary>
    [Fact]
    public async Task AppendAsync_WhenAnEntryDeclaresADifferentAddressThanTheSession_ReturnsTypedFailure()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(375));
        var foreignAddress = new SessionAddress(descriptor.Address.AgentId, new SessionId(Guid.NewGuid()));

        // The entry's self-declared Address names a different session than the one this request addresses.
        // This must be rejected rather than committed under the addressed session while the stored payload's
        // own Address disagrees with where it actually lives.
        var mismatched = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("address-mismatch"),
            [MessageEntry(descriptor, 376, 1, "mismatched-address", address: foreignAddress)]);

        var result = await store.AppendAsync(
            await AuthorizeAsync(fixture, mismatched, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendFailed>();
    }

    /// <summary>Verifies a caller cannot combine an issued old version with a later branch tip and present it as captured evidence.</summary>
    [Fact]
    public async Task ReadAsync_WhenSnapshotVersionAndUpperSequencePairWasNeverIssued_ReturnsTypedFailure()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(35));
        var emptyRead = new SessionReadRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), 1);
        var issued = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, emptyRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var entries = ImmutableArray.Create<SessionEntry>(
            MessageEntry(descriptor, 36, 1, "one"),
            MessageEntry(descriptor, 38, 2, "two"),
            MessageEntry(descriptor, 40, 3, "three"));
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append-after-snapshot"), entries);
        _ = await store.AppendAsync(
            await AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var forged = new SessionReadSnapshot(
            descriptor.Address, descriptor.ActiveBranchId,
            issued.Snapshot!.Version, new SessionSequence(3));
        var continuedRead = new SessionReadRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), 10, forged);

        var result = await store.ReadAsync(
            await AuthorizeAsync(fixture, continuedRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionReadFailed>();
    }

    /// <summary>Verifies a fully authorized foreign tenant observes absence and cannot mutate the owner's record.</summary>
    [Fact]
    public async Task Operations_WhenTenantDiffers_MaskExistenceAndPreserveOwnerState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var foreignIdentity = Identity("tenant-foreign", "foreign-user");
        var foreign = SessionContext(descriptor.Address, foreignIdentity, Correlation(40));
        var foreignEntry = MessageEntry(descriptor, 41, 1, "foreign");
        var append = new SessionAppendRequest(
            foreign, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("foreign-append"), [foreignEntry]);

        var loadResult = await store.LoadAsync(
            await AuthorizeAsync(fixture, foreign, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var appendResult = await store.AppendAsync(
            await AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var ownerContext = SessionContext(descriptor.Address, Identity(), Correlation(42));
        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, ownerContext);

        _ = loadResult.ShouldBeOfType<SessionNotFound>();
        _ = appendResult.ShouldBeOfType<SessionAppendNotFound>();
        page.Entries.ShouldBeEmpty();
    }

    /// <summary>Verifies lookup returns the durable original and effective payload before preprocessing is repeated.</summary>
    [Fact]
    public async Task LookupInputAsync_WhenInputWasAdmitted_ReturnsCompleteReplayBeforePreprocessing()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, context, _, admission, _) = await ProvisionAndAdmitAsync(fixture, store, 50, "lookup");
        var lookup = new SessionInputLookupRequest(
            context, admission.OriginalPayload,
            admission.Preprocessing.OriginalFingerprint);

        var result = await store.LookupInputAsync(
            await AuthorizeAsync(fixture, lookup, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        var replay = result.ShouldBeOfType<SessionInputReplayFound>();
        replay.AdmittedInput.OriginalPayload.ShouldBeEquivalentTo(admission.OriginalPayload);
        replay.AdmittedInput.EffectivePayload.ShouldBeEquivalentTo(admission.EffectivePayload);
        replay.AdmittedInput.Preprocessing.ShouldBe(admission.Preprocessing);
        replay.AdmittedInput.Identity.ShouldBe(context.Identity);
        replay.Receipt.Existing.ShouldBeTrue();
    }

    /// <summary>Verifies promotion advances the single canonical admission so lookup by input identity observes the promoted sequence.</summary>
    [Fact]
    public async Task LookupInputAsync_WhenAdmissionWasPromoted_ReportsPromotedSequence()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 100, "promoted-lookup");
        var start = StartRequest(prepared, 110);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var lookup = new SessionInputLookupRequest(
            prepared.Context, prepared.Admission.OriginalPayload,
            prepared.Admission.Preprocessing.OriginalFingerprint);

        var result = await store.LookupInputAsync(
            await AuthorizeAsync(fixture, lookup, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(fixture, store, prepared.Descriptor, prepared.Context);

        var promotionSequence = page.Entries.OfType<InputPromotedSessionEntry>().Single().Sequence;
        accepted.Existing.ShouldBeFalse();
        var replay = result.ShouldBeOfType<SessionInputReplayFound>();
        replay.AdmittedInput.AdmissionId.ShouldBe(prepared.Admission.AdmissionId);
        replay.AdmittedInput.PromotedSequence.ShouldBe(promotionSequence);
        replay.Receipt.AdmittedSequence.ShouldBe(prepared.Accepted.Receipt.AdmittedSequence);
    }

    /// <summary>Verifies lane provisioning, admission, run acceptance, replay, and recovery retain one exact accepted state.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenPlanIsCurrent_CommitsAndRecoversCompleteAcceptedState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 60, "accept");
        var start = StartRequest(prepared, 70);

        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var replay = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var inRunContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value,
            prepared.Context.Identity,
            new InRunOperationCorrelation(prepared.Context.Correlation.OperationId, start.RunId, start.InitialTurnId));
        var load = new SessionRunStateRequest(inRunContext);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(
            await AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(
            fixture, store, prepared.Descriptor, inRunContext);

        accepted.Existing.ShouldBeFalse();
        replay.Existing.ShouldBeTrue();
        replay.State.ShouldBeEquivalentTo(accepted.State);
        loaded.State.ShouldBeEquivalentTo(accepted.State);
        accepted.State.State.ShouldBe(DurableOperationState.Accepted);
        accepted.State.InitiatingAdmissionId.ShouldBe(prepared.Admission.AdmissionId);
        loaded.State.InitiatingAdmissionId.ShouldBe(prepared.Admission.AdmissionId);
        accepted.State.PromotedAdmissionIds.ShouldBe([prepared.Admission.AdmissionId]);
        accepted.State.Configuration.ShouldBe(start.Configuration);
        accepted.State.SessionProfile.ShouldBe(start.SessionProfile);
        page.Entries.OfType<InputPromotedSessionEntry>().Single()
            .InitiatingAdmissionId.ShouldBe(prepared.Admission.AdmissionId);
        page.Entries.Count(static entry => entry is MessageSessionEntry).ShouldBe(1);
        page.Entries[^1].ShouldBeOfType<OperationAcceptedSessionEntry>().State
            .ShouldBeEquivalentTo(accepted.State);
    }

    /// <summary>Verifies discovery of a lane that was never provisioned reports a typed not-provisioned result.</summary>
    [Fact]
    public async Task LoadLaneStateAsync_WhenLaneWasNeverProvisioned_ReturnsNotProvisioned()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = LaneContext(
            descriptor.Address, Identifier<ExecutionLaneId>(600), Identity(),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(601), null));
        var load = new SessionLaneStateRequest(context);

        var result = await store.LoadLaneStateAsync(
            await AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLaneStateNotProvisioned>();
    }

    /// <summary>Verifies discovery of a freshly provisioned, still idle lane reports the revision and cursor advanced by its one pending admission.</summary>
    [Fact]
    public async Task LoadLaneStateAsync_WhenLaneIsProvisionedAndIdle_ReturnsRevisionAndCursorWithoutAcceptedRun()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context, provisioned, admission, _) =
            await ProvisionAndAdmitAsync(fixture, store, 610, "lane-state-idle");
        var load = new SessionLaneStateRequest(context);

        var loaded = (SessionLaneStateLoaded) await store.LoadLaneStateAsync(
            await AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.State.ExecutionLaneId.ShouldBe(context.ExecutionLaneId!.Value);
        loaded.State.Revision.ShouldBe(new SessionLaneRevision(provisioned.LaneRevision.Value + 1));
        loaded.State.BranchCursor.ShouldBe(new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId));
        loaded.State.AcceptedState.ShouldBeNull();
    }

    /// <summary>Verifies discovery of a lane holding an accepted run reports its current revision, cursor, and the exact accepted state.</summary>
    [Fact]
    public async Task LoadLaneStateAsync_WhenLaneHoldsAnAcceptedRun_ReturnsMatchingAcceptedState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 620, "lane-state-accepted");
        var start = StartRequest(prepared, 630);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var load = new SessionLaneStateRequest(prepared.Context);

        var loaded = (SessionLaneStateLoaded) await store.LoadLaneStateAsync(
            await AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.State.ExecutionLaneId.ShouldBe(prepared.Context.ExecutionLaneId!.Value);
        loaded.State.Revision.ShouldBe(accepted.State.LaneRevision);
        loaded.State.BranchCursor.ShouldBe(accepted.State.CommittedCursor);
        loaded.State.AcceptedState.ShouldBeEquivalentTo(accepted.State);
    }

    /// <summary>Verifies a durably admitted, not-yet-promoted input is discoverable in admitted order.</summary>
    [Fact]
    public async Task LoadPendingInputsAsync_WhenLaneHoldsAnUnpromotedAdmission_ReportsItInAdmittedOrder()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, context, _, _, acceptedInput) = await ProvisionAndAdmitAsync(fixture, store, 900, "pending-idle");
        var load = new SessionPendingInputsRequest(context);

        var loaded = (SessionPendingInputsLoaded) await store.LoadPendingInputsAsync(
            await AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.Pending.Length.ShouldBe(1);
        loaded.Pending[0].AdmissionId.ShouldBe(acceptedInput.Receipt.AdmissionId);
        loaded.Pending[0].PromotedSequence.ShouldBeNull();
    }

    /// <summary>Verifies acceptance consuming the lane's only admission leaves nothing pending.</summary>
    [Fact]
    public async Task LoadPendingInputsAsync_AfterRunAcceptedConsumesTheOnlyAdmission_ReportsEmpty()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 910, "pending-consumed");
        var start = StartRequest(prepared, 920);
        _ = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var load = new SessionPendingInputsRequest(prepared.Context);

        var loaded = (SessionPendingInputsLoaded) await store.LoadPendingInputsAsync(
            await AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.Pending.ShouldBeEmpty();
    }

    /// <summary>Verifies a durably admitted input can be atomically promoted into an already-accepted run's active turn.</summary>
    [Fact]
    public async Task PromoteInputAsync_WhenSelectionIsCurrent_CommitsIntoTheActiveTurnAndAdvancesState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 930, "promote-accept");
        var start = StartRequest(prepared, 940);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var steering = AdmissionRequest(
            prepared.Context, Identifier<AdmissionId>(950), Identifier<InputId>(951),
            Identifier<SessionEntryId>(952), new SessionVersion(prepared.Provisioned.SessionVersion.Value + 2),
            accepted.State.LaneRevision, accepted.State.CommittedCursor, "steer");
        var admittedSteering = (AcceptedInput) await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, steering, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var laneAfterSteering = (SessionLaneStateLoaded) await store.LoadLaneStateAsync(
            await AuthorizeAsync(fixture, new SessionLaneStateRequest(prepared.Context),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var promote = PromoteRequest(prepared, accepted, admittedSteering, laneAfterSteering.State, [steering.AdmissionId], 960);

        var promoted = (SessionInputPromoted) await store.PromoteInputAsync(
            await AuthorizeAsync(fixture, promote, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var replay = await store.PromoteInputAsync(
            await AuthorizeAsync(fixture, promote, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var inRunContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            accepted.State.Correlation);
        var runState = (SessionRunStateLoaded) await store.LoadRunStateAsync(
            await AuthorizeAsync(fixture, new SessionRunStateRequest(inRunContext),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(fixture, store, prepared.Descriptor, prepared.Context);

        promoted.Promoted.Length.ShouldBe(1);
        promoted.Promoted[0].AdmissionId.ShouldBe(steering.AdmissionId);
        _ = promoted.Promoted[0].PromotedSequence.ShouldNotBeNull();
        promoted.OperationStateRevision.ShouldBe(new OperationStateRevision(accepted.State.OperationStateRevision.Value + 1));
        replay.ShouldBeEquivalentTo(promoted);
        runState.State.OperationStateRevision.ShouldBe(promoted.OperationStateRevision);
        runState.State.CommittedCursor.ShouldBe(promoted.CommittedCursor);
        page.Entries.OfType<InputPromotedSessionEntry>().Count().ShouldBe(2);
        page.Entries.OfType<InputPromotedSessionEntry>().Last().AdmissionIds.ShouldBe([steering.AdmissionId]);
        page.Entries.Count(static entry => entry is MessageSessionEntry).ShouldBe(2);
    }

    /// <summary>Verifies promotion against a lane holding no accepted run is rejected without mutation.</summary>
    [Fact]
    public async Task PromoteInputAsync_WhenLaneHasNoAcceptedRun_ReturnsTypedRejection()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context, provisioned, admission, acceptedInput) =
            await ProvisionAndAdmitAsync(fixture, store, 970, "promote-no-run");
        var inRunCorrelation = new InRunOperationCorrelation(
            context.Correlation.OperationId, Identifier<RunId>(980), Identifier<TurnId>(981));
        var inRunContext = LaneContext(
            descriptor.Address, context.ExecutionLaneId!.Value, context.Identity, inRunCorrelation);
        var promote = new SessionInputPromotionRequest(
            inRunContext, [acceptedInput.Receipt.AdmissionId], acceptedInput.Receipt.AdmittedSequence,
            provisioned.LaneRevision, provisioned.SessionVersion,
            new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId),
            Identifier<SessionEntryId>(982), [Identifier<SessionEntryId>(983)], [Identifier<MessageId>(984)],
            new OperationStateRevision(1), Timestamp(970), new IdempotencyKey("promote-no-run"));

        var result = await store.PromoteInputAsync(
            await AuthorizeAsync(fixture, promote, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionInputPromotionRejected>();
    }

    /// <summary>Verifies a stale expected state revision is rejected without mutating the accepted run.</summary>
    [Fact]
    public async Task PromoteInputAsync_WhenExpectedStateRevisionIsStale_ReturnsTypedRejection()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 990, "promote-stale");
        var start = StartRequest(prepared, 1000);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var steering = AdmissionRequest(
            prepared.Context, Identifier<AdmissionId>(1010), Identifier<InputId>(1011),
            Identifier<SessionEntryId>(1012), new SessionVersion(prepared.Provisioned.SessionVersion.Value + 2),
            accepted.State.LaneRevision, accepted.State.CommittedCursor, "stale-steer");
        var admittedSteering = (AcceptedInput) await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, steering, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var laneAfterSteering = (SessionLaneStateLoaded) await store.LoadLaneStateAsync(
            await AuthorizeAsync(fixture, new SessionLaneStateRequest(prepared.Context),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var inRunContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            accepted.State.Correlation);
        var stalePromote = new SessionInputPromotionRequest(
            inRunContext, [steering.AdmissionId], admittedSteering.Receipt.AdmittedSequence,
            laneAfterSteering.State.Revision, new SessionVersion(prepared.Provisioned.SessionVersion.Value + 3),
            laneAfterSteering.State.BranchCursor, Identifier<SessionEntryId>(1020), [Identifier<SessionEntryId>(1021)],
            [Identifier<MessageId>(1022)], new OperationStateRevision(accepted.State.OperationStateRevision.Value + 5),
            Timestamp(1000), new IdempotencyKey("promote-stale"));

        var result = await store.PromoteInputAsync(
            await AuthorizeAsync(fixture, stalePromote, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionInputPromotionRejected>();
    }

    /// <summary>Verifies a lane-owned append advances the lane cursor so a later run acceptance can name the real branch tip.</summary>
    [Fact]
    public async Task AcceptRunAsync_AfterAppendOnLaneBranch_UsesAdvancedCursor()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 220, "lane-append");
        var versionAfterAdmission = new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1);
        var appendedEntry = MessageEntry(prepared.Descriptor, 230, 3, "lane-note");
        var append = new SessionAppendRequest(
            prepared.Context, prepared.Descriptor.ActiveBranchId, versionAfterAdmission,
            new IdempotencyKey("lane-note"), [appendedEntry]);
        var appended = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var advancedCursor = new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, appendedEntry.Id);
        var start = StartRequest(prepared, 240, appended.NewVersion, advancedCursor);

        var result = await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(fixture, store, prepared.Descriptor, prepared.Context);

        var accepted = result.ShouldBeOfType<SessionRunAccepted>();
        accepted.State.PreviousCursor.ShouldBe(advancedCursor);
        accepted.State.CommittedCursor.ShouldBe(new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, start.AcceptedEntryId));
        page.Entries.Select(static entry => entry.GetType()).ShouldBe([
            typeof(ExecutionLaneProvisionedSessionEntry), typeof(InputAdmittedSessionEntry), typeof(MessageSessionEntry),
            typeof(InputPromotedSessionEntry), typeof(MessageSessionEntry), typeof(OperationAcceptedSessionEntry),
        ]);
        page.Entries.OfType<InputPromotedSessionEntry>().Single().CausalParentId.ShouldBe(appendedEntry.Id);
    }

    /// <summary>Verifies acceptance is single-shot per lane: an occupied lane reports the installed owner to any later start.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenLaneAlreadyHoldsAcceptedRun_ReturnsBusyForLaterStart()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 250, "single-shot");
        var first = StartRequest(prepared, 260);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, first, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var secondContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            new BeforeRunOperationCorrelation(Identifier<OperationId>(270), null));
        var secondAdmission = AdmissionRequest(
            secondContext, Identifier<AdmissionId>(271), Identifier<InputId>(272), Identifier<SessionEntryId>(273),
            accepted.SessionVersion, accepted.State.LaneRevision, accepted.State.CommittedCursor, "second");
        var secondAccepted = (AcceptedInput) await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, secondAdmission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var second = new SessionRunStartRequest(
            secondContext, secondAdmission.AdmissionId, [secondAdmission.AdmissionId],
            secondAccepted.Receipt.AdmittedSequence, new SessionLaneRevision(accepted.State.LaneRevision.Value + 1),
            new SessionVersion(accepted.SessionVersion.Value + 1),
            new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, secondAdmission.EntryId),
            null, Identifier<RunId>(280), Identifier<TurnId>(281), Identifier<SessionEntryId>(282),
            [Identifier<SessionEntryId>(283)], [Identifier<MessageId>(284)], Identifier<SessionEntryId>(285),
            new OperationStateRevision(1), Profile(), Configuration(),
            Authorization(prepared.Descriptor.Address.AgentId, prepared.Descriptor.Address.SessionId,
                new InRunOperationCorrelation(secondContext.Correlation.OperationId, Identifier<RunId>(280), Identifier<TurnId>(281)),
                prepared.Context.Identity),
            Timestamp(280), new IdempotencyKey("second-accept"));

        var result = await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, second, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionRunStartBusy(accepted.State.Correlation.OperationId, accepted.State.Correlation.RunId));
    }

    /// <summary>Verifies releasing a lane's accepted run frees it so a later start on the same lane succeeds instead of observing <see cref="SessionRunStartBusy"/>.</summary>
    [Fact]
    public async Task ReleaseRunAsync_AfterAcceptedRun_AllowsANewStartOnTheLane()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 400, "release-start");
        var start = StartRequest(prepared, 410);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var releaseContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            accepted.State.Correlation);
        var release = new SessionRunReleaseRequest(
            releaseContext, accepted.State.OperationStateRevision, accepted.SessionVersion,
            new IdempotencyKey("release-1"));

        var released = await store.ReleaseRunAsync(
            await AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var releasedResult = released.ShouldBeOfType<SessionRunReleased>();
        releasedResult.Existing.ShouldBeFalse();

        var secondContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            new BeforeRunOperationCorrelation(Identifier<OperationId>(420), null));
        var secondAdmission = AdmissionRequest(
            secondContext, Identifier<AdmissionId>(421), Identifier<InputId>(422), Identifier<SessionEntryId>(423),
            releasedResult.NewVersion, accepted.State.LaneRevision, accepted.State.CommittedCursor, "second");
        var secondAccepted = (AcceptedInput) await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, secondAdmission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var second = new SessionRunStartRequest(
            secondContext, secondAdmission.AdmissionId, [secondAdmission.AdmissionId],
            secondAccepted.Receipt.AdmittedSequence, new SessionLaneRevision(accepted.State.LaneRevision.Value + 1),
            new SessionVersion(releasedResult.NewVersion.Value + 1),
            new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, secondAdmission.EntryId),
            null, Identifier<RunId>(430), Identifier<TurnId>(431), Identifier<SessionEntryId>(432),
            [Identifier<SessionEntryId>(433)], [Identifier<MessageId>(434)], Identifier<SessionEntryId>(435),
            new OperationStateRevision(1), Profile(), Configuration(),
            Authorization(prepared.Descriptor.Address.AgentId, prepared.Descriptor.Address.SessionId,
                new InRunOperationCorrelation(secondContext.Correlation.OperationId, Identifier<RunId>(430), Identifier<TurnId>(431)),
                prepared.Context.Identity),
            Timestamp(430), new IdempotencyKey("second-accept"));

        var result = await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, second, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunAccepted>();
    }

    /// <summary>Verifies a release naming a different run than the lane's actual installed occupant is fenced rather than clearing the real owner.</summary>
    [Fact]
    public async Task ReleaseRunAsync_WhenCorrelationDoesNotMatchOccupant_ReturnsFenced()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 450, "release-fenced");
        var start = StartRequest(prepared, 460);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var staleCorrelation = new InRunOperationCorrelation(
            accepted.State.Correlation.OperationId, Identifier<RunId>(999), null);
        var releaseContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            staleCorrelation);
        var release = new SessionRunReleaseRequest(
            releaseContext, accepted.State.OperationStateRevision, accepted.SessionVersion,
            new IdempotencyKey("release-stale"));

        var result = await store.ReleaseRunAsync(
            await AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(
            await AuthorizeAsync(fixture,
                new SessionRunStateRequest(LaneContext(
                    prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
                    accepted.State.Correlation)),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<SessionRunReleaseRejected>();
        rejected.Kind.ShouldBe(SessionRunReleaseRejectionKind.Fenced);
        loaded.State.ShouldBeEquivalentTo(accepted.State);
    }

    /// <summary>Verifies releasing a lane that currently holds no accepted run is a typed rejection rather than a silent no-op success.</summary>
    [Fact]
    public async Task ReleaseRunAsync_WhenLaneHasNoAcceptedRun_ReturnsTypedRejection()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (Descriptor, Context, Provisioned, Admission, Accepted) = await ProvisionAndAdmitAsync(fixture, store, 470, "release-none");
        var releaseContext = LaneContext(
            Descriptor.Address, Context.ExecutionLaneId!.Value, Context.Identity,
            new InRunOperationCorrelation(Context.Correlation.OperationId, Identifier<RunId>(471), null));
        var release = new SessionRunReleaseRequest(
            releaseContext, new OperationStateRevision(1),
            new SessionVersion(Provisioned.SessionVersion.Value + 1), new IdempotencyKey("release-none"));

        var result = await store.ReleaseRunAsync(
            await AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunReleaseRejected>().Kind.ShouldBe(SessionRunReleaseRejectionKind.NoAcceptedRun);
    }

    /// <summary>Verifies a retried release with the same idempotency key returns the original result instead of a second commit.</summary>
    [Fact]
    public async Task ReleaseRunAsync_WhenRetriedWithSameIdempotencyKey_ReturnsSameResult()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 480, "release-retry");
        var start = StartRequest(prepared, 490);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var releaseContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            accepted.State.Correlation);
        var release = new SessionRunReleaseRequest(
            releaseContext, accepted.State.OperationStateRevision, accepted.SessionVersion,
            new IdempotencyKey("release-retry-key"));

        var first = await store.ReleaseRunAsync(
            await AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var second = await store.ReleaseRunAsync(
            await AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var firstReleased = first.ShouldBeOfType<SessionRunReleased>();
        var secondReleased = second.ShouldBeOfType<SessionRunReleased>();
        firstReleased.Existing.ShouldBeFalse();
        secondReleased.Existing.ShouldBeTrue();
        secondReleased.NewVersion.ShouldBe(firstReleased.NewVersion);
    }

    /// <summary>Verifies a release presenting a stale expected session version is a typed conflict without clearing the lane.</summary>
    [Fact]
    public async Task ReleaseRunAsync_WhenExpectedVersionIsStale_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 500, "release-stale-version");
        var start = StartRequest(prepared, 510);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var releaseContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            accepted.State.Correlation);
        var release = new SessionRunReleaseRequest(
            releaseContext, accepted.State.OperationStateRevision,
            new SessionVersion(accepted.SessionVersion.Value + 41), new IdempotencyKey("release-stale-version"));

        var result = await store.ReleaseRunAsync(
            await AuthorizeAsync(fixture, release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(
            await AuthorizeAsync(fixture, new SessionRunStateRequest(releaseContext),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunReleaseRejected>().Kind.ShouldBe(SessionRunReleaseRejectionKind.SessionVersion);
        loaded.State.ShouldBeEquivalentTo(accepted.State);
    }

    /// <summary>Verifies an admission identity collision leaves version, lane cursor, and sequence available to the next valid commit.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenAdmissionIdentityCollides_DoesNotAdvanceState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context, provisioned, admission, firstAccepted) =
            await ProvisionAndAdmitAsync(fixture, store, 80, "first");
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentLaneRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(
            descriptor.ActiveBranchId, admission.EntryId);
        var collision = AdmissionRequest(
            context, admission.AdmissionId, Identifier<InputId>(90), Identifier<SessionEntryId>(91),
            currentVersion, currentLaneRevision, currentCursor, "collision");
        var subsequent = AdmissionRequest(
            context, Identifier<AdmissionId>(92), Identifier<InputId>(93), Identifier<SessionEntryId>(94),
            currentVersion, currentLaneRevision, currentCursor, "subsequent");

        var rejected = await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, collision, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var accepted = await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, subsequent, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = rejected.ShouldBeOfType<InputConflict>();
        accepted.ShouldBeOfType<AcceptedInput>().Receipt.AdmittedSequence.Value
            .ShouldBe(firstAccepted.Receipt.AdmittedSequence.Value + 1);
    }

    /// <summary>Verifies pre-cancelled callers observe cancellation before any protected creation effect.</summary>
    [Fact]
    public async Task CreateAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateStoreRequest();
        var authorized = await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.CreateAsync(authorized, cancellation.Token));
        var context = SessionContext(request.Address, Identity(), Correlation(130));
        var loaded = await store.LoadAsync(
            await AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        exception.CancellationToken.ShouldBe(cancellation.Token);
        _ = loaded.ShouldBeOfType<SessionNotFound>();
    }

    /// <summary>Verifies sequential appends commit in append order with one version step per append.</summary>
    [Fact]
    public async Task AppendAsync_WhenAppendedTwiceInSequence_OrdersEntriesByAppendOrder()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 2);

        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);
        var loaded = (SessionLoaded) await store.LoadAsync(
            await AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.Descriptor.Version.ShouldBe(new SessionVersion(2));
        page.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2]);
        page.Entries.Cast<MessageSessionEntry>().Select(static entry => entry.Message.Parts.OfType<TextPart>().Single().Text)
            .ShouldBe(["seed-0", "seed-1"]);
    }

    /// <summary>Verifies an exact retried append returns the original receipt without duplicating history.</summary>
    [Fact]
    public async Task AppendAsync_WhenRetriedWithSameIdempotencyKey_DoesNotDuplicateEntries()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(140));
        var request = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("dupe"),
            [MessageEntry(descriptor, 141, 1, "once")]);

        var first = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var second = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);

        second.NewVersion.ShouldBe(first.NewVersion);
        _ = page.Entries.ShouldHaveSingleItem();
    }

    /// <summary>Verifies two racing appends at one expected version yield exactly one commit and one typed conflict.</summary>
    [Fact]
    public async Task AppendAsync_WhenConcurrentAppendsShareExpectedVersion_YieldsOneSuccessAndOneConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(150));
        var racerA = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("racer-a"),
            [MessageEntry(descriptor, 151, 1, "a")]);
        var racerB = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("racer-b"),
            [MessageEntry(descriptor, 154, 1, "b")]);
        var authorizedA = await AuthorizeAsync(fixture, racerA, SecurityOperationKind.StateMutation, SecurityEffect.Append);
        var authorizedB = await AuthorizeAsync(fixture, racerB, SecurityOperationKind.StateMutation, SecurityEffect.Append);

        var results = await Task.WhenAll(
            store.AppendAsync(authorizedA, TestContext.Current.CancellationToken).AsTask(),
            store.AppendAsync(authorizedB, TestContext.Current.CancellationToken).AsTask());
        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);

        results.OfType<SessionAppended>().Count().ShouldBe(1);
        results.OfType<SessionAppendConflict>().Count().ShouldBe(1);
        _ = page.Entries.ShouldHaveSingleItem();
    }

    /// <summary>Verifies a page smaller than the branch reports a continuation cursor and more data.</summary>
    [Fact]
    public async Task ReadAsync_WhenPageSizeSmallerThanTotal_ReturnsPartialPageWithHasMoreTrue()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 5);
        var read = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2);

        var page = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        page.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2]);
        page.HasMore.ShouldBeTrue();
        page.ThroughSequence.ShouldBe(new SessionSequence(2));
    }

    /// <summary>Verifies continuing from the previous cursor returns the next contiguous page.</summary>
    [Fact]
    public async Task ReadAsync_WhenContinuingFromPreviousPage_ReturnsRemainingEntries()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 5);
        var firstRead = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2);
        var firstPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, firstRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var secondRead = new SessionReadRequest(context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 2);

        var secondPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, secondRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        secondPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([3, 4]);
        secondPage.HasMore.ShouldBeTrue();
        secondPage.ThroughSequence.ShouldBe(new SessionSequence(4));
    }

    /// <summary>Verifies an issued snapshot pins a continuation to the prefix visible when the first page was read.</summary>
    [Fact]
    public async Task ReadAsync_WhenAppendOccursBetweenPages_ContinuationRetainsOriginalPrefix()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 3);
        var firstRead = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2);
        var firstPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, firstRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var later = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(3), new IdempotencyKey("later"),
            [MessageEntry(descriptor, 160, 4, "later")]);
        _ = (await store.AppendAsync(
            await AuthorizeAsync(fixture, later, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        var issued = firstPage.Snapshot.ShouldNotBeNull();
        var reconstructed = new SessionReadSnapshot(issued.Address, issued.BranchId, issued.Version, issued.UpperSequence);
        var secondRead = new SessionReadRequest(
            context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 10, reconstructed);

        var secondPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, secondRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        secondPage.Snapshot.ShouldBe(reconstructed);
        secondPage.Entries.ShouldHaveSingleItem().Sequence.ShouldBe(new SessionSequence(3));
        secondPage.HasMore.ShouldBeFalse();
    }

    /// <summary>Verifies one multi-entry append advances the version once while the snapshot tracks the last sequence.</summary>
    [Fact]
    public async Task ReadAsync_WhenOneAppendCommitsMultipleEntries_PreservesDistinctVersionAndUpperSequence()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(170));
        var batch = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("batch"),
            [MessageEntry(descriptor, 171, 1, "one"), MessageEntry(descriptor, 174, 2, "two")]);

        var appended = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, batch, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);

        appended.NewVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
        page.Snapshot.ShouldNotBeNull().Version.ShouldBe(appended.NewVersion);
        page.Snapshot.UpperSequence.ShouldBe(new SessionSequence(2));
    }

    /// <summary>Verifies an empty branch reads as an empty page without more data.</summary>
    [Fact]
    public async Task ReadAsync_WhenBranchIsEmpty_ReturnsEmptyPageWithHasMoreFalse()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(180));

        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);

        page.Entries.ShouldBeEmpty();
        page.HasMore.ShouldBeFalse();
        page.ThroughSequence.ShouldBe(new SessionSequence(0));
    }

    /// <summary>Verifies a fresh read cursor beyond the branch tip is a typed failure rather than an empty page.</summary>
    [Fact]
    public async Task ReadAsync_WhenNewCursorIsBeyondBranchTip_ReturnsTypedFailure()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 1);
        var read = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), 10);

        var result = await store.ReadAsync(
            await AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionReadFailed>();
    }

    /// <summary>Verifies a caller-authored snapshot claiming future state is rejected.</summary>
    [Fact]
    public async Task ReadAsync_WhenSnapshotClaimsFutureState_ReturnsTypedFailure()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 1);
        var forged = new SessionReadSnapshot(
            descriptor.Address, descriptor.ActiveBranchId, new SessionVersion(9), new SessionSequence(9));
        var read = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10, forged);

        var result = await store.ReadAsync(
            await AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionReadFailed>();
    }

    /// <summary>Verifies reading an unknown branch of an existing session reports not found.</summary>
    [Fact]
    public async Task ReadAsync_WhenBranchDoesNotExist_ReturnsNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(190));
        var read = new SessionReadRequest(context, Identifier<BranchId>(191), new SessionSequence(0), 10);

        var result = await store.ReadAsync(
            await AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionReadNotFound>();
    }

    /// <summary>Verifies a midway fork copies only the parent entries up to and including the fork sequence.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenForkingMidway_CreatesBranchWithOnlyEntriesUpToForkPoint()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 4);
        var fork = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), new IdempotencyKey("b1"));

        var branched = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var read = new SessionReadRequest(context, branched.NewBranchId, new SessionSequence(0), 10);
        var page = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        branched.ForkedAtSequence.ShouldBe(new SessionSequence(2));
        page.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2]);
    }

    /// <summary>Verifies appending to a fork never changes the original branch, and the fork continues from its own branch-local tip rather than the whole-session entry count.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenForkIsAppended_LeavesOriginalBranchUnchanged()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 4);
        var fork = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), new IdempotencyKey("b1"));
        var branched = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        // The forked branch copied only entries 1 and 2, so its own branch-local tip is 2 even though the
        // main branch (still untouched) has 4 entries and the session version has since advanced past that.
        // The next append on the fork must be sequence 3, not a value derived from the main branch or version.
        var append = new SessionAppendRequest(
            context, branched.NewBranchId, new SessionVersion(5), new IdempotencyKey("nb1"),
            [MessageEntry(descriptor, 200, 3, "new-branch-only", branched.NewBranchId)]);

        var appended = await store.AppendAsync(
            await AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var originalPage = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);
        var branchRead = new SessionReadRequest(context, branched.NewBranchId, new SessionSequence(0), 10);
        var branchPage = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, branchRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        appended.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(new SessionVersion(6));
        originalPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2, 3, 4]);
        branchPage.Entries.Select(static entry => entry.Sequence.Value).ShouldBe([1, 2, 3]);
    }

    /// <summary>Verifies an exact retried fork returns the originally created branch.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenRetriedWithSameIdempotencyKey_ReturnsSameBranch()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 2);
        var fork = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(1), new IdempotencyKey("dupe"));

        var first = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var second = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var loaded = (SessionLoaded) await store.LoadAsync(
            await AuthorizeAsync(fixture, context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        second.ShouldBe(first);
        loaded.Descriptor.Version.ShouldBe(new SessionVersion(3));
    }

    /// <summary>Verifies a replayed fork key with different evidence is rejected and the original receipt survives.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenReplayCarriesChangedForkPoint_RejectsAndPreservesOriginalReceipt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 2);
        var key = new IdempotencyKey("branch-evidence");
        var original = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(1), key);
        var changed = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), key);

        var first = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, original, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var rejected = await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, changed, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var replay = await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, original, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        _ = rejected.ShouldBeOfType<SessionBranchFailed>();
        first.ForkedAtSequence.ShouldBe(new SessionSequence(1));
        replay.ShouldBe(first);
    }

    /// <summary>Verifies a fork sequence beyond the parent tip reports the parent as not found.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenForkPointExceedsParentLength_ReturnsParentNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 2);
        var fork = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(10), new IdempotencyKey("b1"));

        var result = await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionBranchParentNotFound(descriptor.ActiveBranchId, new SessionSequence(10)));
    }

    /// <summary>Verifies forking from an unknown parent branch reports the parent as not found.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenParentBranchDoesNotExist_ReturnsParentNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(210));
        var unknown = Identifier<BranchId>(211);
        var fork = new SessionBranchRequest(context, unknown, new SessionSequence(0), new IdempotencyKey("b1"));

        var result = await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionBranchParentNotFound(unknown, new SessionSequence(0)));
    }

    /// <summary>Verifies forking at sequence zero creates an empty branch.</summary>
    [Fact]
    public async Task CreateBranchAsync_WhenForkingAtZero_CreatesEmptyBranch()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context) = await SeedAsync(fixture, store, 3);
        var fork = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("b1"));

        var branched = (SessionBranched) await store.CreateBranchAsync(
            await AuthorizeAsync(fixture, fork, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var read = new SessionReadRequest(context, branched.NewBranchId, new SessionSequence(0), 10);
        var page = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, read, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        page.Entries.ShouldBeEmpty();
        page.HasMore.ShouldBeFalse();
    }

    private static async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
        TFixture fixture, TRequest request, SecurityOperationKind kind, SecurityEffect effect)
        where TRequest : class =>
        await fixture.AuthorizeAsync(request, kind, effect, TestContext.Current.CancellationToken);

    private static async ValueTask<SessionDescriptor> CreateSessionAsync(TFixture fixture, ISessionStore store)
    {
        var request = CreateStoreRequest();
        var result = await store.CreateAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        return result.ShouldBeOfType<SessionCreated>().Descriptor;
    }

    /// <summary>Creates one session and commits <paramref name="entryCount"/> single-entry appends numbered from sequence one.</summary>
    private static async ValueTask<(SessionDescriptor Descriptor, SessionOperationContext Context)> SeedAsync(
        TFixture fixture, ISessionStore store, int entryCount)
    {
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(1000));
        for (var index = 0; index < entryCount; index++)
        {
            var append = new SessionAppendRequest(
                context, descriptor.ActiveBranchId, new SessionVersion(descriptor.Version.Value + index),
                new IdempotencyKey($"seed-{index}"),
                [MessageEntry(descriptor, 1010 + (index * 3), index + 1, $"seed-{index}")]);
            _ = (await store.AppendAsync(
                await AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        }

        return (descriptor, context);
    }

    private static async ValueTask<SessionPageResult> ReadAllAsync(
        TFixture fixture, ISessionStore store, SessionDescriptor descriptor, SessionOperationContext context)
    {
        var request = new SessionReadRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), 100);
        return await store.ReadAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
    }

    private static async ValueTask<(
        SessionDescriptor Descriptor,
        SessionOperationContext Context,
        SessionExecutionLaneProvisioned Provisioned,
        SessionInputAdmissionRequest Admission,
        AcceptedInput Accepted)> ProvisionAndAdmitAsync(
        TFixture fixture, ISessionStore store, int offset, string key)
    {
        var descriptor = await CreateSessionAsync(fixture, store);
        var identity = Identity();
        var laneId = Identifier<ExecutionLaneId>(offset);
        var context = LaneContext(
            descriptor.Address, laneId, identity,
            new BeforeRunOperationCorrelation(Identifier<OperationId>(offset + 1), null));
        var profile = Profile();
        var configuration = Configuration();
        var provision = new SessionExecutionLaneProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(offset + 2), profile, configuration,
            Timestamp(offset), new IdempotencyKey($"{key}-provision"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var admission = AdmissionRequest(
            context, Identifier<AdmissionId>(offset + 3), Identifier<InputId>(offset + 4),
            Identifier<SessionEntryId>(offset + 5), provisioned.SessionVersion,
            provisioned.LaneRevision, provisioned.BranchCursor, key);
        var accepted = (AcceptedInput) await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        return (descriptor, context, provisioned, admission, accepted);
    }

    private static SessionStoreCreateRequest CreateStoreRequest()
    {
        var agentId = Identifier<AgentId>(1);
        var identity = Identity();
        var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
        var authorization = Authorization(agentId, null, correlation, identity);
        var logical = new SessionCreateRequest(
            agentId, identity, authorization, Identifier<ConversationId>(3),
            new IdempotencyKey("create"), ExtensionData.Empty);
        var address = new SessionAddress(agentId, Identifier<SessionId>(4));
        var context = new SessionOperationContext(
            address.AgentId, address.SessionId, null, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));
        return new SessionStoreCreateRequest(logical, address, context);
    }

    private static SessionRunStartRequest StartRequest(
        (SessionDescriptor Descriptor, SessionOperationContext Context,
            SessionExecutionLaneProvisioned Provisioned, SessionInputAdmissionRequest Admission,
            AcceptedInput Accepted) prepared,
        int offset,
        SessionVersion? expectedVersion = null,
        SessionBranchCursor? branchCursor = null)
    {
        var runId = Identifier<RunId>(offset);
        var turnId = Identifier<TurnId>(offset + 1);
        var inRunCorrelation = new InRunOperationCorrelation(
            prepared.Context.Correlation.OperationId, runId, turnId);
        var inRunAuthorization = Authorization(
            prepared.Descriptor.Address.AgentId, prepared.Descriptor.Address.SessionId,
            inRunCorrelation, prepared.Context.Identity);
        return new SessionRunStartRequest(
            prepared.Context, prepared.Admission.AdmissionId, [prepared.Admission.AdmissionId],
            prepared.Accepted.Receipt.AdmittedSequence,
            new SessionLaneRevision(prepared.Provisioned.LaneRevision.Value + 1),
            expectedVersion ?? new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1),
            branchCursor ?? new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, prepared.Admission.EntryId),
            null, runId, turnId, Identifier<SessionEntryId>(offset + 2),
            [Identifier<SessionEntryId>(offset + 3)], [Identifier<MessageId>(offset + 4)],
            Identifier<SessionEntryId>(offset + 5), new OperationStateRevision(1),
            Profile(), Configuration(), inRunAuthorization, Timestamp(offset),
            new IdempotencyKey("accept"));
    }

    private static SessionInputPromotionRequest PromoteRequest(
        (SessionDescriptor Descriptor, SessionOperationContext Context,
            SessionExecutionLaneProvisioned Provisioned, SessionInputAdmissionRequest Admission,
            AcceptedInput Accepted) prepared,
        SessionRunAccepted accepted,
        AcceptedInput admittedSteering,
        SessionLaneState laneState,
        ImmutableArray<AdmissionId> selected,
        int offset)
    {
        var inRunContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value, prepared.Context.Identity,
            accepted.State.Correlation);
        return new SessionInputPromotionRequest(
            inRunContext, selected, admittedSteering.Receipt.AdmittedSequence, laneState.Revision,
            new SessionVersion(prepared.Provisioned.SessionVersion.Value + 3), laneState.BranchCursor,
            Identifier<SessionEntryId>(offset), [Identifier<SessionEntryId>(offset + 1)],
            [Identifier<MessageId>(offset + 2)], accepted.State.OperationStateRevision, Timestamp(offset),
            new IdempotencyKey("promote"));
    }

    private static SessionInputAdmissionRequest AdmissionRequest(
        SessionOperationContext context, AdmissionId admissionId, InputId inputId,
        SessionEntryId entryId, SessionVersion version, SessionLaneRevision laneRevision,
        SessionBranchCursor cursor, string key)
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
            new IdempotencyKey($"{key}-admit"), 8);
    }

    private static MessageSessionEntry MessageEntry(
        SessionDescriptor descriptor, int offset, long sequence, string text, BranchId? branchId = null, SessionAddress? address = null)
    {
        var correlation = Correlation(offset);
        var branch = branchId ?? descriptor.ActiveBranchId;
        var entryAddress = address ?? descriptor.Address;
        var message = new UserMessage(
            Identifier<MessageId>(offset + 1), descriptor.Address.AgentId,
            descriptor.Address.SessionId, descriptor.ConversationId, branch,
            correlation.RunId, null, Timestamp(offset), MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new MessageSessionEntry(
            Identifier<SessionEntryId>(offset + 2), entryAddress, correlation,
            branch, new SessionSequence(sequence), null, Timestamp(offset),
            new SchemaVersion("1"), message);
    }

    private static SessionOperationContext SessionContext(
        SessionAddress address, ExecutionIdentity identity, OperationCorrelation correlation) =>
        new(address.AgentId, address.SessionId, null, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));

    private static SessionOperationContext LaneContext(
        SessionAddress address, ExecutionLaneId laneId, ExecutionIdentity identity,
        OperationCorrelation correlation) =>
        new(address.AgentId, address.SessionId, laneId, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));

    private static SecurityAuthorizationContext Authorization(
        AgentId agentId, SessionId? sessionId, OperationCorrelation correlation,
        ExecutionIdentity identity) =>
        new(new SecurityProfileKey("conformance"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                new SecurityPolicyVersion(1), new ContentHash("sha256:conformance-policy")),
            new ComponentKey<ISecurityAuthority>("conformance"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    private static ExecutionIdentity Identity(
        string tenant = "tenant-owner", string principal = "owner") =>
        TestExecutionIdentity.Create(
            new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    private static InRunOperationCorrelation Correlation(int offset) =>
        new(Identifier<OperationId>(offset), Identifier<RunId>(offset + 1), null);

    private static SessionProfileReference Profile() =>
        new(new SessionProfileKey("conformance"), new SessionProfileVersion(1));

    private static RunConfigurationReference Configuration() =>
        new(new ConfigurationVersion(1), new RunPolicyVersion(1),
            new ContentHash("sha256:conformance-configuration"));

    private static DateTimeOffset Timestamp(int offset) =>
        DateTimeOffset.UnixEpoch.AddSeconds(Math.Abs((long) offset) + 1);

    private static T Identifier<T>(int value)
    {
        var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        return typeof(T) switch
        {
            var type when type == typeof(AgentId) => (T) (object) new AgentId(guid),
            var type when type == typeof(SessionId) => (T) (object) new SessionId(guid),
            var type when type == typeof(ConversationId) => (T) (object) new ConversationId(guid),
            var type when type == typeof(OperationId) => (T) (object) new OperationId(guid),
            var type when type == typeof(RunId) => (T) (object) new RunId(guid),
            var type when type == typeof(TurnId) => (T) (object) new TurnId(guid),
            var type when type == typeof(ExecutionLaneId) => (T) (object) new ExecutionLaneId(guid),
            var type when type == typeof(BranchId) => (T) (object) new BranchId(guid),
            var type when type == typeof(SessionEntryId) => (T) (object) new SessionEntryId(guid),
            var type when type == typeof(MessageId) => (T) (object) new MessageId(guid),
            var type when type == typeof(AdmissionId) => (T) (object) new AdmissionId(guid),
            var type when type == typeof(InputId) => (T) (object) new InputId(guid),
            var type when type == typeof(SecurityPolicySnapshotId) =>
                (T) (object) new SecurityPolicySnapshotId(guid),
            _ => throw new InvalidOperationException($"Unsupported conformance identifier type {typeof(T).FullName}."),
        };
    }
}
