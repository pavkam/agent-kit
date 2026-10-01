// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines portable write, read, page, transition, deletion, isolation, and authorization behavior for <see cref="IMemoryStore"/>.</summary>
/// <typeparam name="TFixture">The adapter-specific isolated fixture.</typeparam>
/// <remarks>Every case is required contract behavior for all memory-store adapters. Durability cases run when the fixture declares <see cref="ConformanceCapabilities.SupportsDurability"/>.</remarks>
public abstract class MemoryStoreConformanceTests<TFixture>
    where TFixture : IMemoryStoreConformanceFixture, new()
{
    /// <summary>Verifies a new record persists at the initial version.</summary>
    [Fact]
    public async Task WriteAsync_WhenRecordIsNew_PersistsItAtTheInitialVersion()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);

        var result = await WriteAsync(fixture, owner, record, "k");

        result.Replayed.ShouldBeFalse();
        result.Record.ShouldBe(record);
        result.Record!.Version.ShouldBe(new VersionToken("1"));
    }

    /// <summary>Verifies an equivalent write replay returns the original record without duplicating it.</summary>
    [Fact]
    public async Task WriteAsync_WhenReplayedWithTheSameKey_ReturnsTheOriginalWithoutDuplicating()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");

        var replay = await WriteAsync(fixture, owner, record, "k");

        replay.IsWritten.ShouldBeTrue();
        replay.Replayed.ShouldBeTrue();
        replay.Record.ShouldBe(record);
        (await ListAsync(fixture, owner)).Items.Length.ShouldBe(1);
    }

    /// <summary>Verifies reusing a key for a different record is refused.</summary>
    [Fact]
    public async Task WriteAsync_WhenKeyIsReusedForADifferentRecord_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        _ = await WriteAsync(fixture, owner, MemoryTestData.Record(owner), "k");

        var rejected = await WriteAsync(fixture, owner, MemoryTestData.Record(owner, "different"), "k");

        rejected.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies a record identity cannot be reused under a different key.</summary>
    [Fact]
    public async Task WriteAsync_WhenIdentityIsAlreadyInUse_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k1");

        var rejected = await WriteAsync(fixture, owner, record, "k2");

        rejected.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies a grant bound to a different record is denied before anything is written.</summary>
    [Fact]
    public async Task WriteAsync_WhenGrantBindsADifferentRecord_DeniesBeforeAnyWrite()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        var forged = Factory(fixture).Write(MemoryTestData.Record(owner), owner.Authorization, "k");
        var request = new MemoryWriteRequest(record, new IdempotencyKey("k"), forged.Grant);

        var result = await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await ReadAsync(fixture, owner, record.Id)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a consumed single-use grant cannot authorize a second write.</summary>
    [Fact]
    public async Task WriteAsync_WhenGrantWasAlreadyConsumed_Denies()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var request = Factory(fixture).Write(MemoryTestData.Record(owner), owner.Authorization, "k");
        _ = await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken);

        var second = await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken);

        second.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    /// <summary>Verifies an unavailable grant store fails the operation closed without writing.</summary>
    [Fact]
    public async Task WriteAsync_WhenGrantStoreIsUnavailable_DeniesAndWritesNothing()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        var request = Factory(fixture).Write(record, owner.Authorization, "k");
        fixture.Grants.Fail = true;

        var result = await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken);
        fixture.Grants.Fail = false;

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await ReadAsync(fixture, owner, record.Id)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a record claiming another agent than the authorized scope is refused.</summary>
    [Fact]
    public async Task WriteAsync_WhenRecordClaimsAnotherAgent_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var stranger = MemoryTestData.NewOwner();

        var rejected = await WriteAsync(fixture, owner, MemoryTestData.Record(stranger), "k");

        rejected.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.ScopeMismatch);
    }

    /// <summary>Verifies a record claiming another principal than the authorized one is refused.</summary>
    [Fact]
    public async Task WriteAsync_WhenRecordClaimsAnotherPrincipal_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var colleague = MemoryTestData.Owner(owner.AgentId, owner.SessionId, owner.RunId, "tenant", "colleague");

        var rejected = await WriteAsync(fixture, owner, MemoryTestData.Record(colleague), "k");

        rejected.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.ScopeMismatch);
    }

    /// <summary>Verifies the same memory identity in two tenants is two different records.</summary>
    [Fact]
    public async Task WriteAsync_WhenTheSameIdentityIsUsedInAnotherTenant_CreatesAnIndependentRecord()
    {
        var fixture = new TFixture();
        var first = MemoryTestData.NewOwner("tenant-a");
        var second = MemoryTestData.NewOwner("tenant-b");
        var id = new MemoryId(Guid.NewGuid());

        (await WriteAsync(fixture, first, MemoryTestData.Record(first, id: id), "k")).IsWritten.ShouldBeTrue();
        (await WriteAsync(fixture, second, MemoryTestData.Record(second, id: id), "k")).IsWritten.ShouldBeTrue();
    }

    /// <summary>Verifies a read returns the stored record.</summary>
    [Fact]
    public async Task ReadAsync_WhenRecordExists_ReturnsIt()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");

        var read = await ReadAsync(fixture, owner, record.Id);

        read.IsFound.ShouldBeTrue();
        read.Record.ShouldBe(record);
    }

    /// <summary>Verifies a record never written reads as not found.</summary>
    [Fact]
    public async Task ReadAsync_WhenRecordDoesNotExist_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();

        (await ReadAsync(fixture, owner, new MemoryId(Guid.NewGuid()))).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies another tenant cannot observe a record and sees it as absent.</summary>
    [Fact]
    public async Task ReadAsync_WhenRecordBelongsToAnotherTenant_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var outsider = MemoryTestData.NewOwner("tenant-b");
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");

        (await ReadAsync(fixture, outsider, record.Id)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies another agent in the same tenant cannot observe a record.</summary>
    [Fact]
    public async Task ReadAsync_WhenRecordBelongsToAnotherAgentInTheTenant_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var other = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, shared: true);
        _ = await WriteAsync(fixture, owner, record, "k");

        (await ReadAsync(fixture, other, record.Id)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a private record is invisible to another principal of the same agent and a shared record is visible.</summary>
    [Fact]
    public async Task ReadAsync_WhenAnotherPrincipalReadsTheSameAgentsRecord_HonoursTenantSharing()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var colleague = MemoryTestData.Owner(owner.AgentId, owner.SessionId, owner.RunId, "tenant", "colleague");
        var priv = MemoryTestData.Record(owner);
        var shared = MemoryTestData.Record(owner, shared: true);
        _ = await WriteAsync(fixture, owner, priv, "k1");
        _ = await WriteAsync(fixture, owner, shared, "k2");

        (await ReadAsync(fixture, colleague, priv.Id)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
        (await ReadAsync(fixture, colleague, shared.Id)).IsFound.ShouldBeTrue();
    }

    /// <summary>Verifies a read with a grant bound to another record is denied.</summary>
    [Fact]
    public async Task ReadAsync_WhenGrantBindsADifferentRecord_Denies()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");
        var forged = Factory(fixture).Read(new MemoryId(Guid.NewGuid()), owner.Authorization);

        var result = await fixture.Store.ReadAsync(new MemoryReadRequest(record.Id, forged.Grant), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    /// <summary>Verifies pages are returned in stable sequence order with a resumable cursor.</summary>
    [Fact]
    public async Task ListAsync_WhenRecordsExceedThePageSize_PagesInWriteOrderWithACursor()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var records = Enumerable.Range(0, 5).Select(index => MemoryTestData.Record(owner, $"text {index}")).ToArray();
        for (var index = 0; index < records.Length; index++)
        {
            _ = await WriteAsync(fixture, owner, records[index], $"k{index}");
        }

        var first = await ListAsync(fixture, owner, limit: 2);
        var second = await ListAsync(fixture, owner, after: first.NextCursor!.Value, limit: 2);
        var third = await ListAsync(fixture, owner, after: second.NextCursor!.Value, limit: 2);

        first.Items.Select(static item => item.Id).ShouldBe([records[0].Id, records[1].Id]);
        second.Items.Select(static item => item.Id).ShouldBe([records[2].Id, records[3].Id]);
        third.Items.Select(static item => item.Id).ShouldBe([records[4].Id]);
        third.NextCursor.ShouldBeNull();
    }

    /// <summary>Verifies a default page lists active records only.</summary>
    [Fact]
    public async Task ListAsync_WhenStatesAreOmitted_ListsActiveRecordsOnly()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var active = MemoryTestData.Record(owner);
        var proposed = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, active, "k1");
        _ = await WriteAsync(fixture, owner, proposed, "k2");

        (await ListAsync(fixture, owner)).Items.Select(static item => item.Id).ShouldBe([active.Id]);
        (await ListAsync(fixture, owner, states: [MemoryLifecycleState.Proposed])).Items.Select(static item => item.Id).ShouldBe([proposed.Id]);
    }

    /// <summary>Verifies namespace and any-of keyword filters.</summary>
    [Fact]
    public async Task ListAsync_WhenFiltersAreSupplied_FiltersByNamespaceAndKeywordsIgnoringCase()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var coffee = MemoryTestData.Record(owner, "The user likes Coffee.", @namespace: "food");
        var tea = MemoryTestData.Record(owner, "The user likes tea.", @namespace: "food");
        var travel = MemoryTestData.Record(owner, "The user likes trains.", @namespace: "travel");
        _ = await WriteAsync(fixture, owner, coffee, "k1");
        _ = await WriteAsync(fixture, owner, tea, "k2");
        _ = await WriteAsync(fixture, owner, travel, "k3");

        (await ListAsync(fixture, owner, @namespace: new MemoryNamespace("food"))).Items.Select(static item => item.Id).ShouldBe([coffee.Id, tea.Id]);
        (await ListAsync(fixture, owner, terms: ["coffee", "TRAINS"])).Items.Select(static item => item.Id).ShouldBe([coffee.Id, travel.Id]);
        (await ListAsync(fixture, owner, @namespace: new MemoryNamespace("food"), terms: ["trains"])).Items.ShouldBeEmpty();
    }

    /// <summary>Verifies a page never includes another tenant's, another agent's, or another principal's private records.</summary>
    [Fact]
    public async Task ListAsync_WhenOtherScopesHoldRecords_NeverIncludesThem()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var colleague = MemoryTestData.Owner(owner.AgentId, owner.SessionId, owner.RunId, "tenant", "colleague");
        var outsider = MemoryTestData.NewOwner("other-tenant");
        var stranger = MemoryTestData.NewOwner();
        var own = MemoryTestData.Record(owner);
        var shared = MemoryTestData.Record(colleague, shared: true);
        _ = await WriteAsync(fixture, owner, own, "k1");
        _ = await WriteAsync(fixture, colleague, shared, "k2");
        _ = await WriteAsync(fixture, colleague, MemoryTestData.Record(colleague), "k3");
        _ = await WriteAsync(fixture, outsider, MemoryTestData.Record(outsider), "k4");
        _ = await WriteAsync(fixture, stranger, MemoryTestData.Record(stranger, shared: true), "k5");

        (await ListAsync(fixture, owner)).Items.Select(static item => item.Id).ShouldBe([own.Id, shared.Id]);
    }

    /// <summary>Verifies a page read with a grant bound to a different filter is denied.</summary>
    [Fact]
    public async Task ListAsync_WhenGrantBindsADifferentPage_Denies()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var forged = Factory(fixture).List(owner.Authorization, limit: 3);

        var result = await fixture.Store.ListAsync(new MemoryListRequest(null, default, default, 0, 4, forged.Grant), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    /// <summary>Verifies a transition increments the version and records the new state.</summary>
    [Fact]
    public async Task TransitionAsync_WhenVersionMatches_AppliesAndIncrementsTheVersion()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");

        var result = await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Validated, "1");

        result.IsTransitioned.ShouldBeTrue();
        result.Record.State.ShouldBe(MemoryLifecycleState.Validated);
        result.Record.Version.ShouldBe(new VersionToken("2"));
        (await ReadAsync(fixture, owner, record.Id)).Record!.Version.ShouldBe(new VersionToken("2"));
    }

    /// <summary>Verifies a stale expected version is refused.</summary>
    [Fact]
    public async Task TransitionAsync_WhenExpectedVersionIsStale_RejectsWithVersionConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");
        _ = await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Validated, "1", "t1");

        var stale = await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Accepted, "1", "t2");

        stale.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.VersionConflict);
    }

    /// <summary>Verifies a transition outside the lifecycle table is refused.</summary>
    [Fact]
    public async Task TransitionAsync_WhenTransitionIsNotInTheTable_RejectsWithInvalidTransition()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");

        (await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Active, "1")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.InvalidTransition);
    }

    /// <summary>Verifies an equivalent transition replay returns the original result and applies nothing twice.</summary>
    [Fact]
    public async Task TransitionAsync_WhenReplayedWithTheSameKey_ReturnsTheOriginalResult()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");
        var first = await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Validated, "1", "t");

        var replay = await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Validated, "1", "t");

        replay.Replayed.ShouldBeTrue();
        replay.Record.ShouldBe(first.Record);
        (await ReadAsync(fixture, owner, record.Id)).Record!.Version.ShouldBe(new VersionToken("2"));
    }

    /// <summary>Verifies reusing a transition key for a different transition is refused.</summary>
    [Fact]
    public async Task TransitionAsync_WhenKeyIsReusedForADifferentTransition_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");
        _ = await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Validated, "1", "t");

        (await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Rejected, "1", "t")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies a correction atomically appends the active replacement and retires the original.</summary>
    [Fact]
    public async Task TransitionAsync_WhenCorrecting_CreatesTheReplacementAndRetiresTheOriginal()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var original = MemoryTestData.Record(owner, "old fact");
        var replacement = MemoryTestData.Record(owner, "new fact");
        _ = await WriteAsync(fixture, owner, original, "k");

        var result = await TransitionAsync(fixture, owner, original.Id, MemoryLifecycleState.Corrected, "1", replacement: replacement);

        result.IsTransitioned.ShouldBeTrue();
        result.Record.State.ShouldBe(MemoryLifecycleState.Corrected);
        result.Replacement.ShouldBe(replacement);
        (await ListAsync(fixture, owner)).Items.Select(static item => item.Content.Text).ShouldBe(["new fact"]);
        (await ReadAsync(fixture, owner, original.Id)).Record!.State.ShouldBe(MemoryLifecycleState.Corrected);
    }

    /// <summary>Verifies a correction whose replacement identity is already in use applies nothing.</summary>
    [Fact]
    public async Task TransitionAsync_WhenReplacementIdentityIsInUse_RejectsAndLeavesTheOriginalActive()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var original = MemoryTestData.Record(owner);
        var taken = MemoryTestData.Record(owner, "taken");
        _ = await WriteAsync(fixture, owner, original, "k1");
        _ = await WriteAsync(fixture, owner, taken, "k2");

        var result = await TransitionAsync(fixture, owner, original.Id, MemoryLifecycleState.Corrected, "1", replacement: MemoryTestData.Record(owner, "again", id: taken.Id));

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IdempotencyConflict);
        (await ReadAsync(fixture, owner, original.Id)).Record!.State.ShouldBe(MemoryLifecycleState.Active);
    }

    /// <summary>Verifies a correction replacement owned by another agent is refused.</summary>
    [Fact]
    public async Task TransitionAsync_WhenReplacementBelongsToAnotherAgent_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var stranger = MemoryTestData.NewOwner();
        var original = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, original, "k");

        var result = await TransitionAsync(fixture, owner, original.Id, MemoryLifecycleState.Corrected, "1", replacement: MemoryTestData.Record(stranger));

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.ScopeMismatch);
    }

    /// <summary>Verifies another tenant cannot transition a record and sees it as absent.</summary>
    [Fact]
    public async Task TransitionAsync_WhenRecordBelongsToAnotherTenant_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var outsider = MemoryTestData.NewOwner("tenant-b");
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");

        (await TransitionAsync(fixture, outsider, record.Id, MemoryLifecycleState.Validated, "1")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies only the owning principal can transition a shared record.</summary>
    [Fact]
    public async Task TransitionAsync_WhenAnotherPrincipalTransitionsASharedRecord_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var colleague = MemoryTestData.Owner(owner.AgentId, owner.SessionId, owner.RunId, "tenant", "colleague");
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed, shared: true);
        _ = await WriteAsync(fixture, owner, record, "k");

        (await TransitionAsync(fixture, colleague, record.Id, MemoryLifecycleState.Validated, "1")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a transition with a grant bound to different parameters is denied before any change.</summary>
    [Fact]
    public async Task TransitionAsync_WhenGrantBindsADifferentTransition_DeniesBeforeAnyChange()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");
        var forged = Factory(fixture).Transition(record.Id, MemoryLifecycleState.Rejected, "1", owner.Authorization);
        var request = new MemoryTransitionRequest(record.Id, MemoryLifecycleState.Validated, new VersionToken("1"), null, new IdempotencyKey("transition-1"), forged.At, forged.Grant);

        var result = await fixture.Store.TransitionAsync(request, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await ReadAsync(fixture, owner, record.Id)).Record!.State.ShouldBe(MemoryLifecycleState.Proposed);
    }

    /// <summary>Verifies two concurrent transitions from the same version let exactly one win.</summary>
    [Fact]
    public async Task TransitionAsync_WhenTwoTransitionsRaceFromTheSameVersion_ExactlyOneWins()
    {
        var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsConcurrentCreators)
        {
            return;
        }

        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        _ = await WriteAsync(fixture, owner, record, "k");
        var validate = Factory(fixture).Transition(record.Id, MemoryLifecycleState.Validated, "1", owner.Authorization, key: "a");
        var reject = Factory(fixture).Transition(record.Id, MemoryLifecycleState.Rejected, "1", owner.Authorization, key: "b");

        var results = await Task.WhenAll(
            Task.Run(async () => await fixture.Store.TransitionAsync(validate, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
            Task.Run(async () => await fixture.Store.TransitionAsync(reject, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));

        results.Count(static result => result.IsTransitioned).ShouldBe(1);
        results.Single(static result => !result.IsTransitioned).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.VersionConflict);
    }

    /// <summary>Verifies a tombstone makes the record invisible immediately while its purge stays pending.</summary>
    [Fact]
    public async Task DeleteAsync_WhenTombstoning_HidesTheRecordAndNamesThePendingStore()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, "secret text");
        _ = await WriteAsync(fixture, owner, record, "k");

        var deleted = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Tombstone);

        deleted.IsDeleted.ShouldBeTrue();
        deleted.Receipt.LogicallyDeleted.ShouldBeTrue();
        deleted.Receipt.PhysicallyPurged.ShouldBeFalse();
        deleted.Receipt.PendingStores.ShouldBe([fixture.Store.Descriptor.Name]);
        deleted.Receipt.Generation.ShouldBeGreaterThan(0);
        var read = await ReadAsync(fixture, owner, record.Id);
        read.IsFound.ShouldBeFalse();
        read.Tombstone!.Id.ShouldBe(record.Id);
        read.Tombstone.Purged.ShouldBeFalse();
        (await ListAsync(fixture, owner)).Items.ShouldBeEmpty();
    }

    /// <summary>Verifies a purge removes the body and leaves content-free evidence.</summary>
    [Fact]
    public async Task DeleteAsync_WhenPurging_ReportsACompletedPurgeAndKeepsOnlyATombstone()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, "secret text");
        _ = await WriteAsync(fixture, owner, record, "k");

        var deleted = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Purge);

        deleted.Receipt!.PhysicallyPurged.ShouldBeTrue();
        deleted.Receipt.PendingStores.ShouldBeEmpty();
        var read = await ReadAsync(fixture, owner, record.Id);
        read.Tombstone!.Purged.ShouldBeTrue();
        read.Record.ShouldBeNull();
    }

    /// <summary>Verifies a purge may follow an earlier tombstone and that repeating a completed deletion replays its receipt.</summary>
    [Fact]
    public async Task DeleteAsync_WhenPurgingAfterATombstoneAndRepeating_CompletesThenReplays()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");
        var tombstoned = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Tombstone, key: "d1");

        var replayedTombstone = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Tombstone, key: "d2");
        var purged = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Purge, key: "d3");
        var replayedPurge = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Purge, key: "d4");

        replayedTombstone.Replayed.ShouldBeTrue();
        replayedTombstone.Receipt!.Generation.ShouldBe(tombstoned.Receipt!.Generation);
        purged.Replayed.ShouldBeFalse();
        purged.Receipt!.PhysicallyPurged.ShouldBeTrue();
        purged.Receipt.Generation.ShouldBe(tombstoned.Receipt.Generation);
        replayedPurge.Replayed.ShouldBeTrue();
    }

    /// <summary>Verifies every deletion receives a strictly larger deletion generation, which pages report.</summary>
    [Fact]
    public async Task DeleteAsync_WhenSeveralRecordsAreDeleted_AssignsIncreasingGenerationsThatPagesReport()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var first = MemoryTestData.Record(owner);
        var second = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, first, "k1");
        _ = await WriteAsync(fixture, owner, second, "k2");

        var one = await DeleteAsync(fixture, owner, first.Id, MemoryDeleteMode.Tombstone);
        var two = await DeleteAsync(fixture, owner, second.Id, MemoryDeleteMode.Tombstone);

        two.Receipt!.Generation.ShouldBeGreaterThan(one.Receipt!.Generation);
        (await ListAsync(fixture, owner)).DeletionGeneration.ShouldBe(two.Receipt.Generation);
    }

    /// <summary>Verifies a stale expected version blocks deletion of a live record.</summary>
    [Fact]
    public async Task DeleteAsync_WhenExpectedVersionIsStale_RejectsWithVersionConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");

        (await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Tombstone, expectedVersion: "9")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.VersionConflict);
        (await ReadAsync(fixture, owner, record.Id)).IsFound.ShouldBeTrue();
    }

    /// <summary>Verifies another tenant cannot delete a record and sees it as absent.</summary>
    [Fact]
    public async Task DeleteAsync_WhenRecordBelongsToAnotherTenant_ReportsNotFoundAndChangesNothing()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var outsider = MemoryTestData.NewOwner("tenant-b");
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");

        (await DeleteAsync(fixture, outsider, record.Id, MemoryDeleteMode.Purge)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
        (await ReadAsync(fixture, owner, record.Id)).IsFound.ShouldBeTrue();
    }

    /// <summary>Verifies deletion with a grant bound to a different mode is denied before any change.</summary>
    [Fact]
    public async Task DeleteAsync_WhenGrantBindsADifferentMode_DeniesBeforeAnyChange()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");
        var forged = Factory(fixture).Delete(record.Id, MemoryDeleteMode.Tombstone, owner.Authorization);
        var request = new MemoryDeleteRequest(record.Id, null, MemoryDeleteMode.Purge, new IdempotencyKey("delete-1"), forged.At, forged.Grant);

        var result = await fixture.Store.DeleteAsync(request, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await ReadAsync(fixture, owner, record.Id)).IsFound.ShouldBeTrue();
    }

    /// <summary>Verifies a deleted record cannot be transitioned.</summary>
    [Fact]
    public async Task TransitionAsync_WhenRecordIsDeleted_RejectsWithInvalidTransition()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");
        _ = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Tombstone);

        (await TransitionAsync(fixture, owner, record.Id, MemoryLifecycleState.Expired, "1")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.InvalidTransition);
    }

    /// <summary>Verifies a cancelled token stops a write before it commits.</summary>
    [Fact]
    public async Task WriteAsync_WhenCancelled_ThrowsWithoutWriting()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        var request = Factory(fixture).Write(record, owner.Authorization, "k");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await fixture.Store.WriteAsync(request, cancelled.Token));

        (await ReadAsync(fixture, owner, record.Id)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies the descriptor carries its key, audience, and a durability claim consistent with the fixture.</summary>
    [Fact]
    public void Descriptor_WhenRead_NamesItsAudienceAndClaimsDurabilityConsistently()
    {
        var fixture = new TFixture();

        fixture.Store.Descriptor.SecurityAudience.Value.ShouldNotBeNullOrWhiteSpace();
        fixture.Store.Descriptor.Name.ShouldNotBeNullOrWhiteSpace();
        fixture.Store.Descriptor.IsDurable.ShouldBe(fixture.Capabilities.SupportsDurability);
    }

    /// <summary>Verifies acknowledged records, transitions, and tombstones survive reopening a durable store.</summary>
    [Fact]
    public async Task ReopenAsync_WhenTheStoreIsDurable_RetainsRecordsTransitionsAndTombstones()
    {
        var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var owner = MemoryTestData.NewOwner();
        var kept = MemoryTestData.Record(owner, "kept", state: MemoryLifecycleState.Proposed);
        var tombstoned = MemoryTestData.Record(owner, "tombstoned");
        var purged = MemoryTestData.Record(owner, "purged");
        _ = await WriteAsync(fixture, owner, kept, "k1");
        _ = await WriteAsync(fixture, owner, tombstoned, "k2");
        _ = await WriteAsync(fixture, owner, purged, "k3");
        _ = await TransitionAsync(fixture, owner, kept.Id, MemoryLifecycleState.Validated, "1");
        var generation = (await DeleteAsync(fixture, owner, tombstoned.Id, MemoryDeleteMode.Tombstone)).Receipt!.Generation;
        _ = await DeleteAsync(fixture, owner, purged.Id, MemoryDeleteMode.Purge, key: "d2");

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);

        var read = await reopened.ReadAsync(Factory(fixture).Read(kept.Id, owner.Authorization), TestContext.Current.CancellationToken);
        read.Record!.State.ShouldBe(MemoryLifecycleState.Validated);
        read.Record.Version.ShouldBe(new VersionToken("2"));
        var tombstone = await reopened.ReadAsync(Factory(fixture).Read(tombstoned.Id, owner.Authorization), TestContext.Current.CancellationToken);
        tombstone.Tombstone!.Generation.ShouldBe(generation);
        tombstone.Tombstone.Purged.ShouldBeFalse();
        (await reopened.ReadAsync(Factory(fixture).Read(purged.Id, owner.Authorization), TestContext.Current.CancellationToken)).Tombstone!.Purged.ShouldBeTrue();
        var replay = await reopened.WriteAsync(Factory(fixture).Write(kept, owner.Authorization, "k1"), TestContext.Current.CancellationToken);
        replay.Replayed.ShouldBeTrue();
        replay.Record.ShouldBe(kept);
    }

    /// <summary>Verifies a tombstone survives reopening and is never resurrected by a replayed creation.</summary>
    [Fact]
    public async Task ReopenAsync_WhenADeletedRecordIsRewritten_DoesNotResurrectIt()
    {
        var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        _ = await WriteAsync(fixture, owner, record, "k");
        _ = await DeleteAsync(fixture, owner, record.Id, MemoryDeleteMode.Purge);
        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);

        var rewrite = await reopened.WriteAsync(Factory(fixture).Write(record, owner.Authorization, "k"), TestContext.Current.CancellationToken);

        rewrite.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.InvalidTransition);
        (await reopened.ReadAsync(Factory(fixture).Read(record.Id, owner.Authorization), TestContext.Current.CancellationToken)).IsFound.ShouldBeFalse();
    }

    private static MemoryStoreRequestFactory Factory(TFixture fixture) => new(fixture.Grants, fixture.Store.Descriptor.SecurityAudience);

    private static async Task<MemoryWriteResult> WriteAsync(TFixture fixture, MemoryTestOwner owner, DurableMemoryRecord record, string key) =>
        await fixture.Store.WriteAsync(Factory(fixture).Write(record, owner.Authorization, key), TestContext.Current.CancellationToken);

    private static async Task<MemoryReadResult> ReadAsync(TFixture fixture, MemoryTestOwner owner, MemoryId id) =>
        await fixture.Store.ReadAsync(Factory(fixture).Read(id, owner.Authorization), TestContext.Current.CancellationToken);

    private static async Task<MemoryListResult> ListAsync(
        TFixture fixture,
        MemoryTestOwner owner,
        MemoryNamespace? @namespace = null,
        ImmutableArray<MemoryLifecycleState> states = default,
        ImmutableArray<string> terms = default,
        long after = 0,
        int limit = 50) =>
        await fixture.Store.ListAsync(Factory(fixture).List(owner.Authorization, @namespace, states, terms, after, limit), TestContext.Current.CancellationToken);

    private static async Task<MemoryTransitionResult> TransitionAsync(
        TFixture fixture,
        MemoryTestOwner owner,
        MemoryId id,
        MemoryLifecycleState to,
        string expectedVersion,
        string key = "transition-1",
        DurableMemoryRecord? replacement = null) =>
        await fixture.Store.TransitionAsync(Factory(fixture).Transition(id, to, expectedVersion, owner.Authorization, replacement, key), TestContext.Current.CancellationToken);

    private static async Task<MemoryDeleteResult> DeleteAsync(
        TFixture fixture,
        MemoryTestOwner owner,
        MemoryId id,
        MemoryDeleteMode mode,
        string? expectedVersion = null,
        string key = "delete-1") =>
        await fixture.Store.DeleteAsync(Factory(fixture).Delete(id, mode, owner.Authorization, expectedVersion, key), TestContext.Current.CancellationToken);
}
