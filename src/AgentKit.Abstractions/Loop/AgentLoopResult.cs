// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The complete, immutable result of one agent run.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Every message in <see cref="NewMessages"/> is
/// already durably committed by the time this result is returned; this
/// result is a snapshot of what happened, not a pending mutation.
/// </remarks>
public sealed record AgentLoopResult
{
    /// <summary>Initializes a new instance of the <see cref="AgentLoopResult"/> record.</summary>
    /// <param name="agentId">The agent this run belonged to.</param>
    /// <param name="sessionId">The session this run committed to.</param>
    /// <param name="branchId">The branch this run committed to.</param>
    /// <param name="runId">The run this result belongs to.</param>
    /// <param name="outcome">The closed terminal outcome of the run.</param>
    /// <param name="newMessages">Every message this run committed, in commit order.</param>
    /// <param name="finalVersion">
    /// The branch version after every commit this run made, or the version the
    /// run observed when it committed nothing; <see langword="null"/> when the
    /// run settled before it observed the branch at all.
    /// </param>
    /// <param name="output">
    /// The validated structured output a <see cref="RunSucceeded"/> run's selected definition accepted, or
    /// <see langword="null"/> for a free-text run or any outcome other than <see cref="RunSucceeded"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="newMessages"/> is a default, uninitialized array.
    /// </exception>
    public AgentLoopResult(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        AgentRunOutcome outcome,
        ImmutableArray<AgentMessage> newMessages,
        SessionVersion? finalVersion,
        ValidatedOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfDefault(newMessages);

        AgentId = agentId;
        SessionId = sessionId;
        BranchId = branchId;
        RunId = runId;
        Outcome = outcome;
        NewMessages = newMessages;
        FinalVersion = finalVersion;
        Output = output;
    }

    /// <summary>Gets the agent this run belonged to.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session this run committed to.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the branch this run committed to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the run this result belongs to.</summary>
    public RunId RunId { get; init; }

    /// <summary>Gets the closed terminal outcome of the run.</summary>
    public AgentRunOutcome Outcome { get; init; }

    /// <summary>Gets every message this run committed, in commit order.</summary>
    public ImmutableArray<AgentMessage> NewMessages { get; init; }

    /// <summary>Gets the branch version after every commit this run made.</summary>
    /// <value>
    /// The exact version after the run's last commit, or the version the run
    /// observed when loading history if it committed nothing. It is
    /// <see langword="null"/> only when the run settled before observing the
    /// branch (for example, authorization could not be captured at run start or
    /// history could not be loaded); the loop never fabricates a version in
    /// that case.
    /// </value>
    public SessionVersion? FinalVersion { get; init; }

    /// <summary>Gets the validated structured output a successful run's selected definition accepted.</summary>
    /// <value>
    /// The accepted output validated by the run's <see cref="IOutputProcessor"/>, or <see langword="null"/> for a
    /// free-text run or any outcome other than <see cref="RunSucceeded"/>. A run with an output definition never
    /// settles as <see cref="RunSucceeded"/> without it.
    /// </value>
    public ValidatedOutput? Output { get; init; }

    /// <inheritdoc/>
    public bool Equals(AgentLoopResult? other) =>
        other is not null
        && AgentId.Equals(other.AgentId)
        && SessionId.Equals(other.SessionId)
        && BranchId.Equals(other.BranchId)
        && RunId.Equals(other.RunId)
        && Outcome.Equals(other.Outcome)
        && NewMessages.SequenceEqual(other.NewMessages)
        && FinalVersion.Equals(other.FinalVersion)
        && Equals(Output, other.Output);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(SessionId);
        hash.Add(BranchId);
        hash.Add(RunId);
        hash.Add(Outcome);
        foreach (var message in NewMessages)
        {
            hash.Add(message);
        }

        hash.Add(FinalVersion);
        hash.Add(Output);
        return hash.ToHashCode();
    }
}
