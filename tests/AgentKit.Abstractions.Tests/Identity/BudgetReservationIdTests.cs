// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class BudgetReservationIdTests: Conformance.GuidIdentityConformanceTests<BudgetReservationId>
{

    /// <inheritdoc/>
    protected override BudgetReservationId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(BudgetReservationId subject) => subject.Value;
}
