// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one durable lane-bound input eligible for deterministic promotion.</summary>
public sealed record AdmittedInput
{
    /// <summary>Initializes an admitted input snapshot.</summary>
    /// <param name="admissionId">The durable acceptance identity.</param><param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param><param name="executionLaneId">The resolved lane.</param>
    /// <param name="identity">The admitted execution identity.</param><param name="admittedSequence">The positive durable order.</param>
    /// <param name="originalPayload">The original payload.</param><param name="effectivePayload">The captured effective payload.</param>
    /// <param name="preprocessing">The preprocessing evidence.</param><param name="admittedAt">The admission timestamp.</param>
    /// <param name="promotedSequence">The optional committed promotion marker.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, the sequence is zero, or payload identities or delivery classes differ.</exception>
    public AdmittedInput(AdmissionId admissionId, AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        ExecutionIdentity identity, SessionSequence admittedSequence, AgentInput originalPayload, AgentInput effectivePayload,
        InputPreprocessingManifest preprocessing, DateTimeOffset admittedAt, SessionSequence? promotedSequence = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(admissionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfEqual(admittedSequence.Value, 0L, nameof(admittedSequence));
        ArgumentException.ThrowIfInvalidAdmittedInputPayloads(originalPayload, effectivePayload, preprocessing);
        if (promotedSequence is { } promoted)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(promoted.Value, admittedSequence.Value, nameof(promotedSequence));
        }
        AdmissionId = admissionId; AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId;
        Identity = identity; AdmittedSequence = admittedSequence; OriginalPayload = originalPayload; EffectivePayload = effectivePayload;
        Preprocessing = preprocessing; AdmittedAt = admittedAt; PromotedSequence = promotedSequence;
    }

    /// <summary>Gets admission identity.</summary><value>A nondefault durable identity.</value>
    public AdmissionId AdmissionId { get; }
    /// <summary>Gets owning agent.</summary><value>The addressed agent.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets owning session.</summary><value>The addressed session.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets resolved lane.</summary><value>The immutable admitted lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets execution identity.</summary><value>The complete immutable admitted identity.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets admission order.</summary><value>A positive durable session sequence.</value>
    public SessionSequence AdmittedSequence { get; }
    /// <summary>Gets original payload.</summary><value>The caller payload retained for replay comparison.</value>
    public AgentInput OriginalPayload { get; }
    /// <summary>Gets effective payload.</summary><value>The immutable preprocessed payload.</value>
    public AgentInput EffectivePayload { get; }
    /// <summary>Gets preprocessing evidence.</summary><value>The captured version and fingerprints.</value>
    public InputPreprocessingManifest Preprocessing { get; }
    /// <summary>Gets admission time.</summary><value>The captured timestamp.</value>
    public DateTimeOffset AdmittedAt { get; }
    /// <summary>Gets promotion marker.</summary><value>The committed promotion sequence, or null while pending.</value>
    public SessionSequence? PromotedSequence { get; }
}
