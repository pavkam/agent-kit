// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the document pairing one persisted batch member's caller evidence with the identity and expiry the ledger chose.</summary>
public sealed class JsonBudgetReservationEntryTests
{
    /// <summary>Verifies projecting a null domain receipt is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetReservationEntry.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies a round trip through <see cref="JsonBudgetReservationEntry.FromDomain"/> reproduces the original request and allocated identity.</summary>
    [Fact]
    public void FromDomain_WhenProjected_RoundTripsRequestAndIdentity()
    {
        var scope = new BudgetLedgerScopeReference(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null));
        var request = new BudgetReservationRequest(
            scope.Id, new BudgetDimension("tokens"), 2, new BudgetUnit("count"), new OperationId(Guid.NewGuid()), null, new IdempotencyKey("key"));
        var reservationId = new BudgetReservationId(Guid.NewGuid());
        var receipt = new BudgetLedgerReservationReceipt(
            new BudgetLedgerReservationReference(scope, reservationId),
            request,
            new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));

        var entry = JsonBudgetReservationEntry.FromDomain(receipt);

        entry.ToDomainRequest().ShouldBe(request);
        entry.ToDomainReservationId().ShouldBe(reservationId);
        entry.EffectiveExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    /// <summary>Verifies an empty persisted reservation identity is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomainReservationId_WhenIdentityIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var entry = new JsonBudgetReservationEntry(
            new JsonBudgetReservationRequest(Guid.NewGuid(), "tokens", 1, "count", Guid.NewGuid(), null, "key"),
            Guid.Empty,
            DateTimeOffset.UnixEpoch);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => entry.ToDomainReservationId());
    }

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = new JsonBudgetReservationEntry(
            new JsonBudgetReservationRequest(Guid.NewGuid(), "tokens", 1, "count", Guid.NewGuid(), null, "key"),
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetReservationEntry>(
            JsonStoreSerialization.Encode(original, options, 1_024), options);

        decoded.ShouldBe(original);
    }
}
