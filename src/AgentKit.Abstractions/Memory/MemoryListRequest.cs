// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a memory store for one page of the authorized agent's records in stable sequence order.</summary>
/// <remarks>
/// The page covers only the authorized tenant and agent and only records the authorized principal may see. Deleted records
/// never appear. The filter is deliberately small: a namespace, lifecycle states, and any-of keyword terms matched
/// case-insensitively against content. Ranking belongs to retrieval, not the store.
/// </remarks>
public sealed record MemoryListRequest
{
    /// <summary>The largest page size a store accepts.</summary>
    public const int MaximumLimit = 1_000;

    /// <summary>Initializes a validated list request.</summary>
    /// <param name="namespace">The namespace to restrict to, or <see langword="null"/> for every namespace.</param>
    /// <param name="states">The lifecycle states to include, or default for active records only.</param>
    /// <param name="terms">Any-of keyword terms, or default for no text filter.</param>
    /// <param name="afterSequence">The exclusive sequence cursor; zero starts at the beginning.</param>
    /// <param name="limit">The page size, between one and <see cref="MaximumLimit"/>.</param>
    /// <param name="grant">The single-use grant for this exact read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A state is undefined, the cursor is negative, or the limit is out of range.</exception>
    /// <exception cref="ArgumentException">A term is blank, or the grant lacks captured authorization.</exception>
    public MemoryListRequest(
        MemoryNamespace? @namespace,
        ImmutableArray<MemoryLifecycleState> states,
        ImmutableArray<string> terms,
        long afterSequence,
        int limit,
        SecurityGrant grant)
    {
        if (@namespace is { } scope)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope.Value, nameof(@namespace));
        }

        var declaredStates = states.IsDefault ? [MemoryLifecycleState.Active] : states;
        foreach (var state in declaredStates)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(state, nameof(states));
        }

        var declaredTerms = terms.IsDefault ? [] : terms;
        foreach (var term in declaredTerms)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(term, nameof(terms));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaximumLimit);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Namespace = @namespace;
        States = declaredStates;
        Terms = declaredTerms;
        AfterSequence = afterSequence;
        Limit = limit;
        Grant = grant;
    }

    /// <summary>Gets the namespace filter, or <see langword="null"/> for every namespace.</summary>
    public MemoryNamespace? Namespace { get; }

    /// <summary>Gets the lifecycle states to include.</summary>
    public ImmutableArray<MemoryLifecycleState> States { get; }

    /// <summary>Gets the any-of keyword terms.</summary>
    public ImmutableArray<string> Terms { get; }

    /// <summary>Gets the exclusive sequence cursor.</summary>
    public long AfterSequence { get; }

    /// <summary>Gets the page size.</summary>
    public int Limit { get; }

    /// <summary>Gets the single-use grant for this exact read.</summary>
    public SecurityGrant Grant { get; }
}
