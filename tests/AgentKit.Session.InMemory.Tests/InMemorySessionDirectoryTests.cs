// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>Verifies InMemorySessionDirectory behavior and contracts.</summary>
public sealed class InMemorySessionDirectoryTests
{
    private static readonly ComponentId _audience = new("agentkit.session.directory.in-memory-tests");

    [Fact]
    public void Constructor_WhenSecurityAudienceIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new InMemorySessionDirectory(
            default, new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore(),
            new SequenceAuditRecordIds(), TimeProvider.System));
        exception.ParamName.ShouldBe("securityAudience");
    }

    [Fact]
    public void Constructor_WhenAuditDispatcherIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionDirectory(
            _audience, null!, new RecordingGrantStore(), new SequenceAuditRecordIds(), TimeProvider.System));
        exception.ParamName.ShouldBe("auditDispatcher");
    }

    [Fact]
    public void Constructor_WhenGrantStoreIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionDirectory(
            _audience, new RecordingAuditDispatcher(new SecurityAuditAccepted()), null!, new SequenceAuditRecordIds(), TimeProvider.System));
        exception.ParamName.ShouldBe("grantStore");
    }

    [Fact]
    public void Constructor_WhenAuditRecordIdsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionDirectory(
            _audience, new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore(), null!, TimeProvider.System));
        exception.ParamName.ShouldBe("auditRecordIds");
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemorySessionDirectory(
            _audience, new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore(), new SequenceAuditRecordIds(), null!));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Descriptor_WhenAccessed_ReportsNonDurable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        directory.Durable.ShouldBeFalse();
        directory.SecurityAudience.ShouldBe(_audience);
    }

    [Fact]
    public async Task LocateAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await directory.LocateAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task RecordAsync_ThenLocateAsync_ReturnsRecordedLocation()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);

        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocated(location));
    }

    [Fact]
    public async Task LocateAsync_WhenNoRouteRecorded_ReturnsNotFound()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocationNotFound(context.ToAddress()));
    }

    [Fact]
    public async Task LocateAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var context = Context();

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryLookupUnavailable("Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task LocateAsync_WhenAuditDispatcherThrowsUnexpectedException_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new ThrowingAuditDispatcher(), new RecordingGrantStore());
        var context = Context();

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryLookupUnavailable("The directory authorization prerequisite is unavailable."));
    }

    [Fact]
    public async Task LocateAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsDenied()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var context = Context();

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryLookupDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task LocateAsync_WhenGrantStoreReturnsWrongReceipt_DeniesBeforeReadingExistingRoute()
    {
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        grants.ReturnWrongReceipt = true;

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryLookupDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task LocateAsync_WhenCancelledAfterConsumption_ThrowsBeforeReadingExistingRoute()
    {
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        grants.AfterConsume = cancellation.Cancel;

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await directory.LocateAsync(
                new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                    context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task LocateAsync_WhenRouteBelongsToAnotherTenant_ReturnsTenantMaskedMissingResult()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var owner = Context("tenant-a");
        var location = Location(owner.SessionId.ToString(), "store-a", owner.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(owner, location, new IdempotencyKey("record")),
                Grant(owner, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var other = Context("tenant-b", owner.SessionId);

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                other, Grant(other, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocationNotFound(other.ToAddress()));
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenNoRouteRecorded_ReturnsNotFound()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var create = CreateRequest("locate-for-create-missing");

        var result = await directory.LocateForCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionCreateRequest>(
                create, Grant(create, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionCreationLocationNotFound());
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await directory.LocateForCreateAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var create = CreateRequest("locate-for-create-unavailable");

        var result = await directory.LocateForCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionCreateRequest>(
                create, Grant(create, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryCreationLookupUnavailable(
            "Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsDenied()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var create = CreateRequest("locate-for-create-denied");

        var result = await directory.LocateForCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionCreateRequest>(
                create, Grant(create, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryCreationLookupDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenRetryEvidenceDiffers_ReturnsTypedConflictBeforeAllocation()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var original = CreateRequest("retry");
        var location = Location("22222222-2222-2222-2222-222222222222", "store-a");
        _ = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(original, location),
                Grant(original, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var changed = CreateRequest("retry", new ConversationId(Guid.Parse("44444444-4444-4444-4444-444444444444")));

        var result = await directory.LocateForCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionCreateRequest>(
                changed, Grant(changed, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionCreationLocationConflict("The creation retry key was already used with different request evidence."));
    }

    [Fact]
    public async Task RecordAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await directory.RecordAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task RecordAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-unavailable"));

        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteUnavailable("Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task RecordAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsDenied()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-denied"));

        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task RecordAsync_WhenRetriedWithSameIdempotencyKeyAndEvidence_ReturnsExistingLocation()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-replay"));

        var first = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var second = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        first.ShouldBe(new SessionLocationRecorded(location, existing: false));
        second.ShouldBe(new SessionLocationRecorded(location, existing: true));
    }

    [Fact]
    public async Task RecordAsync_WhenRetriedWithSameKeyAndDifferentStoreKey_ReturnsConflict()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var first = new SessionDirectoryWriteRequest(
            context, Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId), new IdempotencyKey("record-diff"));
        var second = new SessionDirectoryWriteRequest(
            context, Location(context.SessionId.ToString(), "store-b", context.Identity.TenantId), new IdempotencyKey("record-diff"));

        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                first, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                second, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionLocationConflict>();
        conflict.Existing.StoreKey.ShouldBe(new SessionStoreKey("store-a"));
        conflict.RequestedStoreKey.ShouldBe(new SessionStoreKey("store-b"));
    }

    [Fact]
    public async Task RecordAsync_WhenExistingLocationBelongsToAnotherOwner_ReturnsDenied()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var owner = Context();
        var location = Location(owner.SessionId.ToString(), "store-a", owner.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(owner, location, new IdempotencyKey("record-owner-1")),
                Grant(owner, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        // Same tenant, different principal, and a fresh idempotency key so the write-route lookup misses.
        var otherIdentity = TestExecutionIdentity.Create(owner.Identity.TenantId, new PrincipalId("someone-else"), ExecutionSubjectKind.Human);
        var otherPrincipal = new SessionOperationContext(
            owner.AgentId, owner.SessionId, null, owner.Correlation, otherIdentity,
            Authorization(owner.AgentId, owner.SessionId, owner.Correlation, otherIdentity));

        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(otherPrincipal, location, new IdempotencyKey("record-owner-2")),
                Grant(otherPrincipal, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("The directory route cannot be recorded."));
    }

    [Fact]
    public async Task RecordAsync_WhenAddressAlreadyBelongsToAnotherTenant_ReturnsDenied()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var owner = Context("tenant-a");
        var ownerLocation = Location(owner.SessionId.ToString(), "store-a", owner.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(owner, ownerLocation, new IdempotencyKey("record-cross-tenant-1")),
                Grant(owner, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        // Different tenant and a fresh idempotency key so the write-route lookup misses and falls through to the address check.
        var otherTenant = Context("tenant-b", owner.SessionId);
        var otherLocation = Location(owner.SessionId.ToString(), "store-a", otherTenant.Identity.TenantId);

        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(otherTenant, otherLocation, new IdempotencyKey("record-cross-tenant-2")),
                Grant(otherTenant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("The directory route cannot be recorded."));
    }

    [Fact]
    public async Task RecordAsync_WhenExistingLocationMatchesOwnerButNewKeyTargetsDifferentStoreKey_ReturnsConflict()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-diff-store-1")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var differentStoreLocation = Location(context.SessionId.ToString(), "store-b", context.Identity.TenantId);

        // A fresh idempotency key against the same address, tenant, and owner but a different store key indexes
        // a new write route, misses the previous-write replay check, and falls through to the store-key conflict.
        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, differentStoreLocation, new IdempotencyKey("record-diff-store-2")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionLocationConflict>();
        conflict.Existing.ShouldBe(location);
        conflict.RequestedStoreKey.ShouldBe(new SessionStoreKey("store-b"));
    }

    [Fact]
    public async Task RecordAsync_WhenExistingLocationMatchesAndNewKeyRetried_ReturnsExistingAsExisting()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-second-key-1")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        // A fresh idempotency key against the exact same address, tenant, owner, and store key indexes a new
        // write route but resolves to the already-committed location.
        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-second-key-2")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocationRecorded(location, existing: true));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await directory.RecordCreateAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task RecordCreateAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var create = CreateRequest("record-create-unavailable");
        var location = Location("22222222-2222-2222-2222-222222222222", "store-a");

        var result = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(create, location),
                Grant(create, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteUnavailable("Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsDenied()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var create = CreateRequest("record-create-denied");
        var location = Location("22222222-2222-2222-2222-222222222222", "store-a");

        var result = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(create, location),
                Grant(create, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenReplayHasEquivalentOriginalRequest_ReturnsWinningRouteWithoutRebinding()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var original = CreateRequest("retry-equivalent");
        var reconstructed = CreateRequest("retry-equivalent");
        var winner = Location("22222222-2222-2222-2222-222222222222", "store-a");
        var candidate = Location("33333333-3333-3333-3333-333333333333", "store-b");

        var first = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(original, winner),
                Grant(original, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var replay = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(reconstructed, candidate),
                Grant(reconstructed, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        first.ShouldBe(new SessionLocationRecorded(winner, existing: false));
        replay.ShouldBe(new SessionLocationRecorded(winner, existing: true));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenAddressAlreadyBelongsToSameTenant_ReturnsConflict()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var winningLocation = Location("22222222-2222-2222-2222-222222222222", "store-a");
        var first = CreateRequest("record-create-conflict-first");
        _ = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(first, winningLocation),
                Grant(first, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var second = CreateRequest("record-create-conflict-second");
        var collidingLocation = Location("22222222-2222-2222-2222-222222222222", "store-b");

        var result = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(second, collidingLocation),
                Grant(second, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionLocationConflict>();
        conflict.Existing.ShouldBe(winningLocation);
        conflict.RequestedStoreKey.ShouldBe(new SessionStoreKey("store-b"));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenAddressAlreadyBelongsToAnotherTenant_ReturnsDenied()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var winningLocation = Location("22222222-2222-2222-2222-222222222222", "store-a", new TenantId("tenant-a"));
        var first = CreateRequest("record-create-cross-tenant-first", tenant: "tenant-a");
        _ = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(first, winningLocation),
                Grant(first, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var second = CreateRequest("record-create-cross-tenant-second", tenant: "tenant-b");
        var collidingLocation = Location("22222222-2222-2222-2222-222222222222", "store-b", new TenantId("tenant-b"));

        var result = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(second, collidingLocation),
                Grant(second, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("The directory route cannot be recorded."));
    }

    [Fact]
    public async Task ListAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await directory.ListAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ListAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var request = ListRequest();

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(request, Grant(request), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryListUnavailable("Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task ListAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsUnavailable()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var request = ListRequest();

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(request, Grant(request), Intent()),
            TestContext.Current.CancellationToken);

        // ListAsync intentionally maps a denial the same way as an unavailability (see InMemorySessionDirectory.ListCoreAsync).
        result.ShouldBe(new SessionDirectoryListUnavailable("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task ListAsync_WhenCancelledAfterConsumption_ThrowsBeforeReadingRoutes()
    {
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var request = ListRequest();
        using var cancellation = new CancellationTokenSource();
        grants.AfterConsume = cancellation.Cancel;

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await directory.ListAsync(
                new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                    request, Grant(request), Intent()), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task ListAsync_WhenRoutesSpanAgentsAndTenants_ReturnsOnlyVisibleOrderedPage()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var first = CreateRequest("list-first");
        var second = CreateRequest("list-second");
        var firstLocation = Location("22222222-2222-2222-2222-222222222222", "store-a");
        var secondLocation = Location("33333333-3333-3333-3333-333333333333", "store-a");
        _ = await directory.RecordCreateAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
            new SessionDirectoryCreateRecordRequest(first, secondLocation),
            Grant(first, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()), TestContext.Current.CancellationToken);
        _ = await directory.RecordCreateAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
            new SessionDirectoryCreateRecordRequest(second, firstLocation),
            Grant(second, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()), TestContext.Current.CancellationToken);
        var request = ListRequest();

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                request, Grant(request), Intent()), TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<SessionDirectoryPage>();
        page.Locations.Select(static location => location.Address.SessionId)
            .ShouldBe([firstLocation.Address.SessionId]);
        page.NextCursor.ShouldBe(firstLocation.Address.SessionId);
    }

    [Fact]
    public async Task ListAsync_WhenObserved_EmitsDirectoryActivityAndLog()
    {
        var logger = new RecordingDirectoryLogger();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore(), logger);
        var request = ListRequest();
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.SessionDirectoryOperation
                && activity.GetTagItem(AgentKitTagNames.SessionOperation)?.Equals("list") == true
                && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(request.AgentId.ToString()) == true);

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                request, Grant(request), Intent()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDirectoryPage>();
        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("success");
        var completed = logger.Snapshot().ShouldHaveSingleItem();
        completed.EventId.Id.ShouldBe(16002);
    }

    [Fact]
    public async Task RecordAsync_WhenObserved_EmitsFailedActivityForTypedRejection()
    {
        var logger = new RecordingDirectoryLogger();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore(), logger);
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-observed"));
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.SessionDirectoryOperation
                && activity.GetTagItem(AgentKitTagNames.SessionOperation)?.Equals("record") == true);

        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        var completed = logger.Snapshot().ShouldHaveSingleItem();
        completed.EventId.Id.ShouldBe(16002);
    }

    private static InMemorySessionDirectory CreateDirectory(
        ISecurityAuditDispatcher audits,
        RecordingGrantStore grants,
        ILogger<InMemorySessionDirectory>? logger = null) =>
        new(_audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System, logger);

    private static SessionOperationContext Context(string tenant = "tenant", SessionId? sessionId = null)
    {
        var identity = TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var id = sessionId ?? new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SessionOperationContext(agentId, id, null, correlation, identity, Authorization(agentId, id, correlation, identity));
    }

    private static SessionCreateRequest CreateRequest(
        string idempotencyKey,
        ConversationId? conversationId = null,
        string tenant = "tenant")
    {
        var identity = TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SessionCreateRequest(agentId, identity, Authorization(agentId, null, correlation, identity), conversationId,
            new IdempotencyKey(idempotencyKey), ExtensionData.Empty);
    }

    private static SessionDirectoryListRequest ListRequest()
    {
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SessionDirectoryListRequest(
            agentId, identity, Authorization(agentId, null, correlation, identity), null, 1);
    }

    private static SecurityAuthorizationContext Authorization(
        AgentId agentId,
        SessionId? sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity) => new(
        new SecurityProfileKey("security"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"),
        new AgentDefinitionRevision(1),
        new ConfigurationVersion(1),
        new SecurityAuthorizationScope(agentId, sessionId, correlation),
        identity);

    private static SessionLocation Location(string sessionId, string storeKey, TenantId? tenantId = null) => new(
        new SessionAddress(
            new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SessionId(Guid.Parse(sessionId))),
        tenantId ?? new TenantId("tenant"),
        new SessionStoreKey(storeKey),
        new SessionDirectoryRevision(1),
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("v1"));

    private static SecurityGrant Grant(SessionOperationContext context, SecurityOperationKind kind, SecurityEffect effect) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), context.Authorization.Scope, context.Identity, _audience,
        kind, effect, [SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress())],
        SessionDirectorySecurityBinding.LocateFingerprint(context), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, 1);

    private static SecurityGrant Grant(SessionCreateRequest request, SecurityOperationKind kind, SecurityEffect effect) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), request.Authorization.Scope, request.Identity, _audience,
        kind, effect, [SessionDirectorySecurityBinding.CreationResource(request.Identity.TenantId, request.AgentId, request.IdempotencyKey)],
        SessionDirectorySecurityBinding.LocateForCreateFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, 1);

    private static SecurityGrant Grant(SessionDirectoryListRequest request) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), request.Authorization.Scope, request.Identity, _audience,
        SecurityOperationKind.StateRead, SecurityEffect.Observe,
        [SessionDirectorySecurityBinding.ListResource(request.Identity.TenantId, request.AgentId)],
        SessionDirectorySecurityBinding.ListFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, 1);

    private static SecurityEnforcementIntent Intent() => new(
        new SecurityEnforcementIntentId(Guid.NewGuid()),
        requiredFence: null);

    /// <summary>An audit dispatcher that always throws, to exercise the directory's catch-all authorization failure path.</summary>
    private sealed class ThrowingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit dispatch failure.");
    }

    private sealed class RecordingAuditDispatcher(SecurityAuditDispatchResult result): ISecurityAuditDispatcher
    {
        private readonly Lock _gate = new();
        private readonly List<SecurityAuditRecord> _records = [];

        public IReadOnlyList<SecurityAuditRecord> Records
        {
            get
            {
                lock (_gate)
                {
                    return [.. _records];
                }
            }
        }

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _records.Add(record);
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingGrantStore: ISecurityGrantStore
    {
        private readonly Lock _gate = new();
        private readonly List<SecurityEnforcementRequest> _enforcements = [];

        public IReadOnlyList<SecurityEnforcementRequest> Enforcements
        {
            get
            {
                lock (_gate)
                {
                    return [.. _enforcements];
                }
            }
        }

        public bool ReturnWrongReceipt { get; set; }
        public Action? AfterConsume { get; set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default)
        {
            Record(enforcement);
            return ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed."));
        }

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default)
        {
            Record(enforcement);
            var receipt = new SecurityEnforcementIntentReceipt(intent.Id, grant.Id, grant.RequestId, enforcement,
                intent.RequiredFence, ReturnWrongReceipt
                    ? new ContentHash("sha256:wrong")
                    : SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch);
            AfterConsume?.Invoke();
            return ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed, 0, "Consumed.", receipt));
        }

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

        private void Record(SecurityEnforcementRequest enforcement)
        {
            lock (_gate)
            {
                _enforcements.Add(enforcement);
            }
        }
    }

    private sealed class SequenceAuditRecordIds: IIdentifierGenerator<SecurityAuditRecordId>
    {
        private int _next;

        public SecurityAuditRecordId Create() => new(new Guid(Interlocked.Increment(ref _next), 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
    }

    private sealed class RecordingDirectoryLogger: ILogger<InMemorySessionDirectory>
    {
        private readonly ConcurrentQueue<(EventId EventId, string Message)> _events = [];

        public ImmutableArray<(EventId EventId, string Message)> Snapshot() => [.. _events];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            _events.Enqueue((eventId, formatter(state, exception)));
        }
    }
}
