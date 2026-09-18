// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Portable JSON mirror of <see cref="BudgetScopeAddress"/>, the structural address every ledger reference is checked against.</summary>
/// <remarks>
/// <para>
/// The address is the isolation boundary the ledger compares before disclosing or mutating anything, so it must survive a
/// storage round trip exactly. Domain identities are unwrapped to their JSON-native primitives here and rebuilt through
/// their own validating constructors on read, so a persisted empty identity or blank tenant fails closed instead of
/// reconstructing a default address that would widen isolation.
/// </para>
/// <para>
/// A null optional member means the scope truthfully sits above that level. Absence is preserved rather than normalized to
/// an empty identity, because a fabricated session, run, or operation binding would change which reservations a boundary
/// is allowed to charge.
/// </para>
/// </remarks>
/// <param name="TenantId">The non-blank tenant partition text.</param>
/// <param name="PrincipalId">The non-blank principal text.</param>
/// <param name="AgentId">The raw value of the non-empty owning agent identity.</param>
/// <param name="SessionId">The raw value of the bound session, or <see langword="null"/> for an address above session scope.</param>
/// <param name="RunId">The raw value of the bound run, or <see langword="null"/> for an address outside an active run.</param>
/// <param name="OperationId">The raw value of the bound operation, or <see langword="null"/> for a reusable parent scope.</param>
public sealed record JsonBudgetScopeAddress(
    string TenantId,
    string PrincipalId,
    Guid AgentId,
    Guid? SessionId,
    Guid? RunId,
    Guid? OperationId)
{
    /// <summary>Projects one domain scope address into its portable JSON representation.</summary>
    /// <param name="value">The non-null address to project.</param>
    /// <returns>A document carrying the unwrapped required identities and each optional identity exactly as present or absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonBudgetScopeAddress FromDomain(BudgetScopeAddress value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonBudgetScopeAddress(
            value.TenantId.Value,
            value.PrincipalId.Value,
            value.AgentId.Value,
            value.SessionId?.Value,
            value.RunId?.Value,
            value.OperationId?.Value);
    }

    /// <summary>Reconstructs the exact domain address this document was projected from.</summary>
    /// <returns>An address equal to the projected original, with every absent optional identity preserved as null.</returns>
    /// <remarks>Every identity is rebuilt through its own validating constructor before <see cref="BudgetScopeAddress"/> revalidates the combination, so persisted bytes are checked rather than trusted.</remarks>
    /// <exception cref="ArgumentException">The persisted tenant or principal text is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted required or present optional identity is empty.</exception>
    public BudgetScopeAddress ToDomain() => new(
        new TenantId(TenantId),
        new PrincipalId(PrincipalId),
        new AgentId(AgentId),
        SessionId is { } session ? new SessionId(session) : null,
        RunId is { } run ? new RunId(run) : null,
        OperationId is { } operation ? new OperationId(operation) : null);
}
