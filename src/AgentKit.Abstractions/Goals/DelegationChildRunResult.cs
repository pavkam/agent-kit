// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports how one child attempt ended.</summary>
/// <remarks>The result is a bounded projection: it carries no transcript, and a summary is only meaningful for a succeeded child. The worker validates it against the delegation's acceptance criteria before the child can complete.</remarks>
public sealed record DelegationChildRunResult
{
    /// <summary>Initializes a validated result.</summary>
    /// <param name="runId">The run the engine admitted for the attempt, or <see langword="null"/> when none was admitted.</param>
    /// <param name="status">The terminal status; <see cref="DelegationStatus.Dispatched"/> is not terminal and is rejected.</param>
    /// <param name="summary">The bounded answer of a succeeded child, or <see langword="null"/>.</param>
    /// <param name="usage">The known usage.</param>
    /// <param name="sideEffectCertainty">The truthful effect certainty.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is a present default value, <paramref name="status"/> is undefined or <see cref="DelegationStatus.Dispatched"/>, or <paramref name="sideEffectCertainty"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="usage"/> is null.</exception>
    public DelegationChildRunResult(RunId? runId, DelegationStatus status, string? summary, GoalBudgetUsage usage, SideEffectCertainty sideEffectCertainty)
    {
        if (runId is { } run)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(run, default, nameof(runId));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentOutOfRangeException.ThrowIfEqual(status, DelegationStatus.Dispatched);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        RunId = runId;
        Status = status;
        Summary = summary;
        Usage = usage;
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets the run the engine admitted for the attempt, or <see langword="null"/> when none was admitted.</summary>
    public RunId? RunId { get; }

    /// <summary>Gets the terminal status.</summary>
    public DelegationStatus Status { get; }

    /// <summary>Gets the bounded answer, or <see langword="null"/>.</summary>
    public string? Summary { get; }

    /// <summary>Gets the known usage.</summary>
    public GoalBudgetUsage Usage { get; }

    /// <summary>Gets the truthful side-effect certainty.</summary>
    public SideEffectCertainty SideEffectCertainty { get; }
}
