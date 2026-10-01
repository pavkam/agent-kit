// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one durable goal: an intended outcome with identity, ownership, status, budget, and a version token.</summary>
/// <remarks>
/// <para>
/// A goal is domain state, never a prompt prefix. <see cref="OwnerAgentId"/>, <see cref="SessionId"/>, and
/// <see cref="OriginatingRunId"/> name the agent, session, and run that own the goal record; a delegated child goal is
/// owned by its delegating parent and the agent that actually executes it is recorded on each <see cref="GoalAttempt"/>.
/// </para>
/// <para>
/// The profile key, profile version, and agent-definition revision are captured at creation so resume and delayed
/// joins use what the goal started under. The record is immutable; every state change is a recorded
/// <see cref="GoalTransition"/> that yields a new instance with a new <see cref="Version"/>.
/// </para>
/// </remarks>
public sealed record AgentGoal
{
    /// <summary>Initializes a validated goal.</summary>
    /// <param name="id">The goal identity.</param>
    /// <param name="parentId">The parent goal, or <see langword="null"/> for a root goal.</param>
    /// <param name="ownerAgentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="originatingRunId">The run that created the goal.</param>
    /// <param name="profileKey">The captured goal profile.</param>
    /// <param name="profileVersion">The captured positive profile revision.</param>
    /// <param name="agentDefinitionRevision">The captured agent-definition revision.</param>
    /// <param name="status">The current defined status.</param>
    /// <param name="definition">The bounded objective.</param>
    /// <param name="budget">The goal's ceiling.</param>
    /// <param name="activeAttemptId">The attempt holding the execution lease, or <see langword="null"/>.</param>
    /// <param name="version">The non-blank optimistic-concurrency token.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="extensions">The forward-compatible extension data.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, the parent equals the goal, the profile version is not positive, the revision is negative, or the status is undefined.</exception>
    /// <exception cref="ArgumentException">The profile key or version token is blank, an <see cref="GoalStatus.Active"/> goal names no attempt, or a <see cref="GoalStatus.Proposed"/> or <see cref="GoalStatus.Ready"/> goal names one.</exception>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public AgentGoal(
        GoalId id,
        GoalId? parentId,
        AgentId ownerAgentId,
        SessionId sessionId,
        RunId originatingRunId,
        GoalProfileKey profileKey,
        GoalProfileVersion profileVersion,
        AgentDefinitionRevision agentDefinitionRevision,
        GoalStatus status,
        GoalDefinition definition,
        GoalBudget budget,
        GoalAttemptId? activeAttemptId,
        VersionToken version,
        DateTimeOffset createdAt,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        if (parentId is { } parent)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(parent, default, nameof(parentId));
            ArgumentOutOfRangeException.ThrowIfEqual(parent, id, nameof(parentId));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(ownerAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(originatingRunId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentOutOfRangeException.ThrowIfNegative(agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(budget);
        if (activeAttemptId is { } attempt)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(attempt, default, nameof(activeAttemptId));
        }

        ArgumentException.ThrowIfNotEqual(status is GoalStatus.Active && activeAttemptId is null, false, nameof(activeAttemptId));
        ArgumentException.ThrowIfNotEqual(status is GoalStatus.Proposed or GoalStatus.Ready && activeAttemptId is not null, false, nameof(activeAttemptId));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentNullException.ThrowIfNull(extensions);
        Id = id;
        ParentId = parentId;
        OwnerAgentId = ownerAgentId;
        SessionId = sessionId;
        OriginatingRunId = originatingRunId;
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        AgentDefinitionRevision = agentDefinitionRevision;
        Status = status;
        Definition = definition;
        Budget = budget;
        ActiveAttemptId = activeAttemptId;
        Version = version;
        CreatedAt = createdAt;
        Extensions = extensions;
    }

    /// <summary>Creates the goal as it stands after a transition, preserving every creation fact.</summary>
    /// <param name="status">The new defined status.</param>
    /// <param name="activeAttemptId">The attempt holding the execution lease, or <see langword="null"/>.</param>
    /// <param name="version">The new optimistic-concurrency token.</param>
    /// <returns>A new goal identical to this one except for status, active attempt, and version.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined or <paramref name="activeAttemptId"/> is default.</exception>
    /// <exception cref="ArgumentException">The status and attempt violate the goal invariants, or <paramref name="version"/> is blank.</exception>
    public AgentGoal WithState(GoalStatus status, GoalAttemptId? activeAttemptId, VersionToken version) =>
        new(Id, ParentId, OwnerAgentId, SessionId, OriginatingRunId, ProfileKey, ProfileVersion, AgentDefinitionRevision,
            status, Definition, Budget, activeAttemptId, version, CreatedAt, Extensions);

    /// <summary>Gets the goal identity.</summary>
    public GoalId Id { get; }

    /// <summary>Gets the parent goal, or <see langword="null"/> for a root goal.</summary>
    public GoalId? ParentId { get; }

    /// <summary>Gets the agent that owns the goal record.</summary>
    public AgentId OwnerAgentId { get; }

    /// <summary>Gets the session that owns the goal record.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the run that created the goal.</summary>
    public RunId OriginatingRunId { get; }

    /// <summary>Gets the captured goal profile key.</summary>
    public GoalProfileKey ProfileKey { get; }

    /// <summary>Gets the captured goal profile revision.</summary>
    public GoalProfileVersion ProfileVersion { get; }

    /// <summary>Gets the captured agent-definition revision.</summary>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }

    /// <summary>Gets the current lifecycle status.</summary>
    public GoalStatus Status { get; }

    /// <summary>Gets the bounded objective.</summary>
    public GoalDefinition Definition { get; }

    /// <summary>Gets the goal's ceiling.</summary>
    public GoalBudget Budget { get; }

    /// <summary>Gets the attempt holding the execution lease, or <see langword="null"/>.</summary>
    public GoalAttemptId? ActiveAttemptId { get; }

    /// <summary>Gets the optimistic-concurrency token; it changes on every transition.</summary>
    public VersionToken Version { get; }

    /// <summary>Gets the creation instant.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the forward-compatible extension data.</summary>
    public ExtensionData Extensions { get; }
}
