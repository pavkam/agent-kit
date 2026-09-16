// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetStartExpired behavior and contracts.</summary>
public sealed class BudgetStartExpiredTests
{
    [Fact]
    public void BudgetStartExpired_WhenConstructed_PreservesExactExpiryEvidence()
    {
        var reservationId = new BudgetReservationId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var expiry = DateTimeOffset.Parse("2026-09-09T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var result = new BudgetStartExpired(reservationId, expiry);
        result.ReservationId.ShouldBe(reservationId);
        result.EffectiveExpiry.ShouldBe(expiry);
    }

    [Fact]
    public void BudgetStartExpired_WhenReservationIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetStartExpired(default, DateTimeOffset.UtcNow));
        exception.ParamName.ShouldBe("reservationId");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetStartExpired(new BudgetReservationId(Guid.Parse("10000000-0000-0000-0000-000000000001")), DateTimeOffset.UnixEpoch);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
