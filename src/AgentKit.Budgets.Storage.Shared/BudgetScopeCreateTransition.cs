// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Storage;

/// <summary>Evaluates scope-creation replay and admission without clocks, identity generation, observation, or persistence effects.</summary>
internal static class BudgetScopeCreateTransition
{
    /// <summary>Returns an exact immutable replay before an adapter consults other collaborators.</summary>
    /// <param name="request">The non-null presented request.</param>
    /// <param name="persistedRequest">The bound request, or null when the key is new.</param>
    /// <param name="persistedReference">The bound reference, present exactly when <paramref name="persistedRequest"/> is present.</param>
    /// <returns>The original receipt, or null when no replay binding exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">Only one persisted replay value is present.</exception>
    /// <exception cref="BudgetLedgerMutationConflictException">The key is bound to different evidence.</exception>
    internal static BudgetLedgerScopeCreateResult? Replay(BudgetLedgerScopeCreateRequest request, BudgetLedgerScopeCreateRequest? persistedRequest, BudgetLedgerScopeReference? persistedReference)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNotEqual(persistedRequest is null, persistedReference is null, nameof(persistedReference));
        return persistedRequest is null
            ? null
            : persistedRequest != request
                ? throw new BudgetLedgerMutationConflictException("The scope key is bound to different evidence.")
                : new BudgetLedgerScopeCreated(persistedReference!);
    }

    /// <summary>Rejects lineage depth before an adapter consults the dimension catalog.</summary>
    /// <param name="request">The non-null new request.</param>
    /// <param name="parent">The captured parent lineage, or null for a root.</param>
    /// <returns>A depth rejection, or null when catalog validation may proceed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    internal static BudgetLedgerScopeCreateRejected? EvaluateDepth(BudgetLedgerScopeCreateRequest request, ScopeCreateParent? parent)
    {
        ArgumentNullException.ThrowIfNull(request);
        return parent is not null && parent.Depth >= request.Admission.MaximumScopeDepth
            ? Rejected(BudgetScopeCreationFailureKind.MaximumDepthExceeded, "The scope would exceed its captured maximum depth.")
            : null;
    }

    /// <summary>Evaluates limit descriptors and inherited constraints from already captured evidence.</summary>
    /// <param name="request">The non-null new request.</param>
    /// <param name="parent">The captured parent lineage, or null for a root.</param>
    /// <param name="descriptors">One captured descriptor result per configured limit.</param>
    /// <returns>A typed limit rejection, or null when identity generation may proceed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="descriptors"/> is default or has the wrong length.</exception>
    internal static BudgetLedgerScopeCreateRejected? EvaluateLimits(BudgetLedgerScopeCreateRequest request, ScopeCreateParent? parent, ImmutableArray<BudgetDimensionDescriptor?> descriptors)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfDefault(descriptors);
        ArgumentException.ThrowIfNotEqual(descriptors.Length, request.OriginalRequest.Limits.Length, nameof(descriptors));
        for (var index = 0; index < request.OriginalRequest.Limits.Length; index++)
        {
            var limit = request.OriginalRequest.Limits[index];
            var descriptor = descriptors[index];
            if (descriptor is null || !descriptor.AllowedUnits.Contains(limit.Unit))
            {
                return Rejected(BudgetScopeCreationFailureKind.InvalidLimit, "The scope limit has no compatible dimension descriptor.");
            }
            foreach (var ancestor in parent?.Ancestors ?? [])
            {
                if (ancestor.OriginalRequest.Limits.Any(item => item.Dimension == limit.Dimension && item.Unit != limit.Unit))
                {
                    return Rejected(BudgetScopeCreationFailureKind.InvalidLimit, "The scope limit unit conflicts with an ancestor limit.");
                }
                var inherited = ancestor.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == limit.Dimension && item.Unit == limit.Unit);
                if (inherited is { Kind: BudgetLimitKind.Hard } && limit.Value > inherited.Value)
                {
                    return Rejected(BudgetScopeCreationFailureKind.LimitWiderThanAncestor, "The scope limit widens an ancestor hard limit.");
                }
            }
        }
        return null;
    }

    /// <summary>Builds a complete immutable insertion after all admission and collaborator work succeeds.</summary>
    /// <param name="request">The non-null admitted request.</param>
    /// <param name="parent">The captured parent lineage, or null for a root.</param>
    /// <param name="generatedId">The nondefault generated scope identity.</param>
    /// <param name="generatedIdExists">Whether authoritative storage already contains the identity.</param>
    /// <param name="nextRevision">The positive preflighted commit revision.</param>
    /// <returns>The immutable public receipt and adapter mutation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="generatedId"/> is default, <paramref name="nextRevision"/> is not positive, or <paramref name="parent"/> was not admitted under the captured maximum depth.</exception>
    /// <exception cref="BudgetLedgerStateException"><paramref name="generatedIdExists"/> is true.</exception>
    internal static (BudgetLedgerScopeCreateResult Result, ScopeCreateMutation Mutation) PlanAccepted(BudgetLedgerScopeCreateRequest request, ScopeCreateParent? parent, BudgetScopeId generatedId, bool generatedIdExists, long nextRevision)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(generatedId, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextRevision);
        if (parent is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(parent.Depth, request.Admission.MaximumScopeDepth, nameof(parent));
        }
        if (generatedIdExists)
        {
            throw new BudgetLedgerStateException("The scope identity source produced a duplicate value.");
        }
        var reference = new BudgetLedgerScopeReference(generatedId, request.OriginalRequest.Address);
        return (new BudgetLedgerScopeCreated(reference), new ScopeCreateMutation(reference, request, parent?.Reference.Id, parent is null ? 1 : parent.Depth + 1, nextRevision));
    }

    private static BudgetLedgerScopeCreateRejected Rejected(BudgetScopeCreationFailureKind kind, string message) => new(new BudgetScopeCreationFailed(kind, message));
}
