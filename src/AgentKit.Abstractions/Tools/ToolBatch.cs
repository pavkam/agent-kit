// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The complete preflighted set of prepared calls one <see cref="IToolScheduler"/> invocation executes together.</summary>
/// <remarks>
/// Under the tool scheduling and concurrency contract, the scheduler preflights the whole batch — identities,
/// validation, hard limits, and side-effect-free permission checks — before starting any call in it, and every
/// accepted entry reaches exactly one terminal result regardless of completion order. <see cref="Entries"/>
/// preserves the provider-emitted source order that defines publication order; the scheduler is free to start
/// entries concurrently subject to their declared and host-verified scheduling policy. This type is an immutable
/// value object with structural equality over its fields and is safe to share across threads without
/// synchronization.
/// </remarks>
public sealed record ToolBatch
{
    /// <summary>Initializes an immutable preflighted tool batch.</summary>
    /// <param name="agentId">The nondefault owning agent shared by every entry.</param>
    /// <param name="sessionId">The nondefault owning session shared by every entry.</param>
    /// <param name="runId">The nondefault active run shared by every entry.</param>
    /// <param name="entries">The initialized entries in provider-emitted source order.</param>
    /// <param name="failureMode">The defined batch failure-settlement policy.</param>
    /// <param name="unknownSchedulingMode">The defined policy for entries with no declared scheduling compatibility.</param>
    /// <param name="deadline">The instant by which the whole batch must settle.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/>, <paramref name="sessionId"/>, or <paramref name="runId"/> is default, or
    /// <paramref name="failureMode"/> or <paramref name="unknownSchedulingMode"/> is undefined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> is uninitialized, contains a null or duplicate-call-identity entry, or contains
    /// an entry whose invocation does not belong to the supplied agent, session, and run.
    /// </exception>
    public ToolBatch(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        ImmutableArray<ToolBatchEntry> entries,
        ToolBatchFailureMode failureMode,
        UnknownSchedulingMode unknownSchedulingMode,
        DateTimeOffset deadline)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentException.ThrowIfContainsNull(entries);
        ArgumentOutOfRangeException.ThrowIfUndefined(failureMode);
        ArgumentOutOfRangeException.ThrowIfUndefined(unknownSchedulingMode);

        var seenCallIds = new HashSet<ToolCallId>();
        foreach (var entry in entries)
        {
            ArgumentException.ThrowIfNotEqual(entry.Invocation.AgentId, agentId, nameof(entries));
            ArgumentException.ThrowIfNotEqual(entry.Invocation.SessionId, sessionId, nameof(entries));
            ArgumentException.ThrowIfNotEqual(entry.Invocation.RunId, runId, nameof(entries));
            ArgumentException.ThrowIfNotEqual(seenCallIds.Add(entry.Invocation.CallId), true, nameof(entries));
        }

        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        Entries = entries;
        FailureMode = failureMode;
        UnknownSchedulingMode = unknownSchedulingMode;
        Deadline = deadline;
    }

    /// <summary>Gets the owning agent shared by every entry.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the owning session shared by every entry.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the active run shared by every entry.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the preflighted entries in provider-emitted source order.</summary>
    /// <value>An initialized sequence with unique call identities; may be empty.</value>
    public ImmutableArray<ToolBatchEntry> Entries { get; }

    /// <summary>Gets the batch failure-settlement policy.</summary>
    /// <value>Whether a failed entry cancels its siblings or leaves them to settle independently.</value>
    public ToolBatchFailureMode FailureMode { get; }

    /// <summary>Gets the policy applied to an entry with no declared scheduling compatibility.</summary>
    public UnknownSchedulingMode UnknownSchedulingMode { get; }

    /// <summary>Gets the instant by which the whole batch must settle.</summary>
    /// <value>The bound the scheduler uses to interrupt calls that never settle.</value>
    public DateTimeOffset Deadline { get; }

    /// <summary>Determines complete structural batch equality.</summary>
    /// <param name="other">The record to compare, or null.</param>
    /// <returns>True when every scalar field and ordered entry is equal.</returns>
    public bool Equals(ToolBatch? other) =>
        other is not null
        && AgentId == other.AgentId
        && SessionId == other.SessionId
        && RunId == other.RunId
        && Entries.SequenceEqual(other.Entries)
        && FailureMode == other.FailureMode
        && UnknownSchedulingMode == other.UnknownSchedulingMode
        && Deadline == other.Deadline;

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over every scalar field and ordered entry.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(SessionId);
        hash.Add(RunId);
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        hash.Add(FailureMode);
        hash.Add(UnknownSchedulingMode);
        hash.Add(Deadline);
        return hash.ToHashCode();
    }
}
