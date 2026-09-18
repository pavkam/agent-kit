// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Portable JSON mirror of <see cref="BudgetLedgerScopeCreateRequest"/>, the complete immutable evidence one scope was admitted under.</summary>
/// <remarks>
/// The ledger answers an idempotent scope replay by comparing the presented request with the persisted one, so the journal
/// must retain the caller's original evidence rather than a summary of the resulting scope. Storing the admission facts in
/// the same document keeps the replay comparison and the capacity bounds inseparable.
/// </remarks>
/// <param name="ParentScopeId">The raw value of the parent scope, or <see langword="null"/> when the request created a root scope.</param>
/// <param name="Address">The non-null structural address the scope was created at.</param>
/// <param name="Limits">The ordered ceilings configured directly on the scope; never default and possibly empty.</param>
/// <param name="IdempotencyKey">The non-blank caller key that replays this exact creation.</param>
/// <param name="Admission">The non-null capacity policy captured when the scope was admitted.</param>
public sealed record JsonBudgetScopeCreateRequest(
    Guid? ParentScopeId,
    JsonBudgetScopeAddress Address,
    ImmutableArray<JsonBudgetLimit> Limits,
    string IdempotencyKey,
    JsonBudgetScopeAdmission Admission)
{
    /// <summary>Projects one domain scope-create transition into its portable JSON representation.</summary>
    /// <param name="value">The non-null admitted request to project.</param>
    /// <returns>A document carrying the original request members in order plus the captured admission facts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonBudgetScopeCreateRequest FromDomain(BudgetLedgerScopeCreateRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var original = value.OriginalRequest;
        ImmutableArray<JsonBudgetLimit> limits = original.Limits.IsDefault
            ? []
            : [.. original.Limits.Select(JsonBudgetLimit.FromDomain)];
        return new JsonBudgetScopeCreateRequest(
            original.ParentScopeId?.Value,
            JsonBudgetScopeAddress.FromDomain(original.Address),
            limits,
            original.IdempotencyKey.Value,
            JsonBudgetScopeAdmission.FromDomain(value.Admission));
    }

    /// <summary>Reconstructs the exact domain scope-create transition this document was projected from.</summary>
    /// <returns>A request equal to the projected original, including limit order.</returns>
    /// <remarks>Every nested value is rebuilt through its own validating constructor, so a hand-edited journal line is rejected rather than admitted as a scope with impossible bounds.</remarks>
    /// <exception cref="ArgumentNullException"><see cref="Address"/> or <see cref="Admission"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">A persisted limit, address member, or the replay key is invalid or repeats a dimension.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted identity is empty, a bound is not positive, or a limit value or kind is out of range.</exception>
    public BudgetLedgerScopeCreateRequest ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Address);
        ArgumentNullException.ThrowIfNull(Admission);
        return new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(
                ParentScopeId is { } parent ? new BudgetScopeId(parent) : null,
                Address.ToDomain(),
                [.. ConfiguredLimits.Select(static limit => limit.ToDomain())],
                new IdempotencyKey(IdempotencyKey)),
            Admission.ToDomain());
    }

    /// <summary>Gets the ordered configured ceilings, normalizing an omitted array to an empty one.</summary>
    /// <returns>The persisted limits in authored order, or an empty sequence when the document carries none.</returns>
    /// <remarks>A decoder produces a default <see cref="ImmutableArray{T}"/> when the member is absent, so callers use this accessor instead of touching <see cref="Limits"/> directly.</remarks>
    public ImmutableArray<JsonBudgetLimit> ConfiguredLimits => Limits.IsDefault ? [] : Limits;

    /// <summary>Compares scope evidence by ordered limit contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate document to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar member and nested document is equal and both limit sequences are element-wise equal in the same order.</returns>
    /// <remarks>Compiler-generated record equality compares <see cref="Limits"/> by backing-array identity, so two documents decoded from byte-identical JSON would otherwise compare unequal and break the initialization round-trip check.</remarks>
    public bool Equals(JsonBudgetScopeCreateRequest? other) =>
        other is not null
        && ParentScopeId == other.ParentScopeId
        && Address == other.Address
        && ConfiguredLimits.AsSpan().SequenceEqual(other.ConfiguredLimits.AsSpan())
        && string.Equals(IdempotencyKey, other.IdempotencyKey, StringComparison.Ordinal)
        && Admission == other.Admission;

    /// <summary>Computes a hash consistent with <see cref="Equals(JsonBudgetScopeCreateRequest?)"/>.</summary>
    /// <returns>A hash derived from every scalar member, nested document, and each ordered limit.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ParentScopeId);
        hash.Add(Address);
        foreach (var limit in ConfiguredLimits.AsSpan())
        {
            hash.Add(limit);
        }

        hash.Add(IdempotencyKey, StringComparer.Ordinal);
        hash.Add(Admission);
        return hash.ToHashCode();
    }
}
