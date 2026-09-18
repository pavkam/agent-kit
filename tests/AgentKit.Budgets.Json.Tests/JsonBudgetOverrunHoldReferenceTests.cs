// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the portable JSON mirror that locates one boundary-specific overrun generation.</summary>
public sealed class JsonBudgetOverrunHoldReferenceTests
{
    private static readonly BudgetLedgerScopeReference _boundary = new(
        new BudgetScopeId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null));
    private static readonly BudgetLedgerReservationReference _reservation = new(
        _boundary, new BudgetReservationId(Guid.Parse("20000000-0000-0000-0000-000000000001")));

    /// <summary>Verifies projecting a null domain reference is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetOverrunHoldReference.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies a round trip through <see cref="JsonBudgetOverrunHoldReference.FromDomain"/> and <see cref="JsonBudgetOverrunHoldReference.ToDomain"/> reproduces an equal domain value when resolved against matching replayed references.</summary>
    [Fact]
    public void ToDomain_WhenResolvedAgainstMatchingReferences_RoundTripsExactly()
    {
        var original = new BudgetOverrunHoldReference(_boundary, _reservation, new BudgetAccountingRevision(3));

        var restored = JsonBudgetOverrunHoldReference.FromDomain(original).ToDomain(_boundary, _reservation);

        restored.ShouldBe(original);
    }

    /// <summary>Verifies a null boundary supplied at reconstruction is refused.</summary>
    [Fact]
    public void ToDomain_WhenBoundaryIsNull_ThrowsArgumentNullException()
    {
        var document = JsonBudgetOverrunHoldReference.FromDomain(
            new BudgetOverrunHoldReference(_boundary, _reservation, new BudgetAccountingRevision(1)));

        Should.Throw<ArgumentNullException>(() => document.ToDomain(null!, _reservation)).ParamName.ShouldBe("boundary");
    }

    /// <summary>Verifies a null reservation supplied at reconstruction is refused.</summary>
    [Fact]
    public void ToDomain_WhenReservationIsNull_ThrowsArgumentNullException()
    {
        var document = JsonBudgetOverrunHoldReference.FromDomain(
            new BudgetOverrunHoldReference(_boundary, _reservation, new BudgetAccountingRevision(1)));

        Should.Throw<ArgumentNullException>(() => document.ToDomain(_boundary, null!)).ParamName.ShouldBe("reservation");
    }

    /// <summary>Verifies a boundary whose identity does not match the persisted identity is refused.</summary>
    [Fact]
    public void ToDomain_WhenBoundaryIdentityDiffers_ThrowsArgumentException()
    {
        var document = JsonBudgetOverrunHoldReference.FromDomain(
            new BudgetOverrunHoldReference(_boundary, _reservation, new BudgetAccountingRevision(1)));
        var otherBoundary = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), _boundary.Address);

        Should.Throw<ArgumentException>(() => document.ToDomain(otherBoundary, _reservation)).ParamName.ShouldBe("boundary");
    }

    /// <summary>Verifies a reservation whose identity does not match the persisted identity is refused.</summary>
    [Fact]
    public void ToDomain_WhenReservationIdentityDiffers_ThrowsArgumentException()
    {
        var document = JsonBudgetOverrunHoldReference.FromDomain(
            new BudgetOverrunHoldReference(_boundary, _reservation, new BudgetAccountingRevision(1)));
        var otherReservation = new BudgetLedgerReservationReference(_boundary, new BudgetReservationId(Guid.NewGuid()));

        Should.Throw<ArgumentException>(() => document.ToDomain(_boundary, otherReservation)).ParamName.ShouldBe("reservation");
    }

    /// <summary>Verifies a non-positive persisted triggering revision is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenTriggeringRevisionIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonBudgetOverrunHoldReference(_boundary.Id.Value, _reservation.Id.Value, 0);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.ToDomain(_boundary, _reservation));
    }

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = new JsonBudgetOverrunHoldReference(Guid.NewGuid(), Guid.NewGuid(), 7);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetOverrunHoldReference>(
            JsonStoreSerialization.Encode(original, options, 1_024), options);

        decoded.ShouldBe(original);
    }
}
