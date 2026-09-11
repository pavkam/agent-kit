// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetEffectiveReservation behavior and contracts.</summary>
public sealed class BudgetEffectiveReservationTests
{
    [Fact]
    public void BudgetEffectiveReservation_WhenExpiryIsSupplied_PreservesAbsoluteInstant()
    {
        var expiresAt = DateTimeOffset.UnixEpoch.AddTicks(1);
        var effectiveReservation = new BudgetEffectiveReservation(expiresAt);
        effectiveReservation.ExpiresAt.ShouldBe(expiresAt);
    }
}
