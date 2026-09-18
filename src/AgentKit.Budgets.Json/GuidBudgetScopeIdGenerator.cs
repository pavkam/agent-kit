// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Creates process-random scope identities for hosts that do not replace the generator.</summary>
/// <remarks>This default exists so a composition is usable without extra wiring; a host needing deterministic or ordered identities registers its own <see cref="IIdentifierGenerator{TIdentifier}"/> before this leaf runs.</remarks>
internal sealed class GuidBudgetScopeIdGenerator: IIdentifierGenerator<BudgetScopeId>
{
    /// <inheritdoc/>
    /// <returns>A nondefault identity backed by a freshly generated globally unique value.</returns>
    public BudgetScopeId Create() => new(Guid.NewGuid());
}
