// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The event raised after retrieval candidates were ranked and authorized and before they are returned for exposure.</summary>
/// <remarks>
/// <see cref="Candidates"/> is the read-only ranked set about to be offered. The only writable member is
/// <see cref="ExcludedPositions"/>: a hook may drop candidates by their zero-based position in <see cref="Candidates"/>. It
/// can neither add a candidate, reorder the set, nor edit a candidate, and a position outside the set is an invalid mutation
/// that fails the retrieval. Excluded candidates are counted as unauthorized in the retrieval summary.
/// </remarks>
public sealed class BeforeRetrievalExposureEventArgs: AgentHookEventArgs, IAgentScopedHookStage
{
    /// <summary>Initializes the event arguments.</summary>
    /// <param name="dispatch">The dispatch identity for <see cref="AgentHookPoints.BeforeRetrievalExposure"/>.</param>
    /// <param name="query">The effective query the candidates answer.</param>
    /// <param name="candidates">The ranked candidates about to be offered.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> or <paramref name="query"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="candidates"/> is the default array or contains null.</exception>
    public BeforeRetrievalExposureEventArgs(HookDispatchMetadata dispatch, RetrievalQuery query, ImmutableArray<RetrievalCandidate> candidates)
        : base(dispatch)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfDefault(candidates);
        ArgumentException.ThrowIfContainsNull(candidates);
        Query = query;
        Candidates = candidates;
        ExcludedPositions = [];
    }

    /// <summary>Gets the agent that issued the query.</summary>
    public AgentId AgentId => Query.Context.AgentId;

    /// <summary>Gets the session the query came from, when it has one.</summary>
    public SessionId? SessionId => Query.Context.SessionId;

    /// <summary>Gets the effective query the candidates answer.</summary>
    public RetrievalQuery Query { get; }

    /// <summary>Gets the ranked candidates about to be offered.</summary>
    public ImmutableArray<RetrievalCandidate> Candidates { get; }

    /// <summary>Gets or sets the zero-based positions of <see cref="Candidates"/> to drop before exposure.</summary>
    public ImmutableArray<int> ExcludedPositions { get; set; }

    /// <summary>Gets the candidates that remain after the exclusions, in their original ranked order.</summary>
    public ImmutableArray<RetrievalCandidate> Remaining
    {
        get
        {
            var excluded = ExcludedPositions.IsDefault ? [] : ExcludedPositions.ToHashSet();
            return [.. Candidates.Where((_, position) => !excluded.Contains(position))];
        }
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (ExcludedPositions.IsDefault)
        {
            throw new HookValidationException("A before-retrieval-exposure hook may not replace the exclusions with a default array.");
        }

        var seen = new HashSet<int>();
        foreach (var position in ExcludedPositions)
        {
            if (position < 0 || position >= Candidates.Length)
            {
                throw new HookValidationException(
                    $"A before-retrieval-exposure hook excluded position {position}, outside the {Candidates.Length} offered candidates.");
            }

            if (!seen.Add(position))
            {
                throw new HookValidationException($"A before-retrieval-exposure hook excluded position {position} more than once.");
            }
        }
    }

    /// <inheritdoc/>
    public override object? CaptureMutableState() => ExcludedPositions;

    /// <inheritdoc/>
    public override void RestoreMutableState(object? snapshot)
    {
        if (snapshot is ImmutableArray<int> captured)
        {
            ExcludedPositions = captured;
        }
    }
}
