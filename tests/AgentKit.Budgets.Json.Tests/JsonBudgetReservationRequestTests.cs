// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the portable JSON mirror of one member of an indivisible reservation batch.</summary>
public sealed class JsonBudgetReservationRequestTests
{
    /// <summary>Verifies projecting a null domain request is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetReservationRequest.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies a caller-supplied expiry round-trips exactly rather than being normalized to a resolved default.</summary>
    [Fact]
    public void ToDomain_WhenExpiresAtIsSupplied_RoundTripsExactly()
    {
        var original = new BudgetReservationRequest(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetDimension("tokens"),
            2,
            new BudgetUnit("count"),
            new OperationId(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch.AddHours(1),
            new IdempotencyKey("key"));

        var restored = JsonBudgetReservationRequest.FromDomain(original).ToDomain();

        restored.ShouldBe(original);
    }

    /// <summary>Verifies an omitted caller expiry is preserved as null rather than fabricated.</summary>
    [Fact]
    public void ToDomain_WhenExpiresAtIsOmitted_PreservesNull()
    {
        var original = new BudgetReservationRequest(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetDimension("tokens"),
            2,
            new BudgetUnit("count"),
            new OperationId(Guid.NewGuid()),
            null,
            new IdempotencyKey("key"));

        var document = JsonBudgetReservationRequest.FromDomain(original);

        document.ExpiresAt.ShouldBeNull();
        document.ToDomain().ExpiresAt.ShouldBeNull();
    }

    /// <summary>Verifies a non-positive persisted amount is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenAmountIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetReservationRequest(
            Guid.NewGuid(), "tokens", 0, "count", Guid.NewGuid(), null, "key").ToDomain());

    /// <summary>Verifies a blank persisted replay key is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenIdempotencyKeyIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new JsonBudgetReservationRequest(
            Guid.NewGuid(), "tokens", 1, "count", Guid.NewGuid(), null, " ").ToDomain());

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = new JsonBudgetReservationRequest(
            Guid.NewGuid(), "tokens", 3, "count", Guid.NewGuid(), DateTimeOffset.UnixEpoch, "key");
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetReservationRequest>(
            JsonStoreSerialization.Encode(original, options, 1_024), options);

        decoded.ShouldBe(original);
    }
}
