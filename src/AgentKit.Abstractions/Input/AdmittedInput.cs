// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one durable lane-bound input whose captured state determines eligibility for deterministic promotion.</summary>
/// <remarks>The record retains original and effective payloads plus the admitted identity snapshot. A present promotion sequence records consumption into a turn; it does not itself prove model processing or run settlement.</remarks>
public sealed record AdmittedInput
{
    /// <summary>Initializes one immutable durable admission snapshot.</summary>
    /// <param name="admissionId">The non-default durable identity assigned at acceptance.</param>
    /// <param name="agentId">The non-default agent definition addressed by the input.</param>
    /// <param name="sessionId">The non-default session that owns the admission record.</param>
    /// <param name="executionLaneId">The non-default resolved lane to which promotion remains bound.</param>
    /// <param name="identity">The non-null complete execution identity captured at admission, not a later ambient identity.</param>
    /// <param name="admittedSequence">The positive durable sequence that orders eligibility for promotion.</param>
    /// <param name="originalPayload">The original caller payload retained for idempotency comparison and provenance.</param>
    /// <param name="effectivePayload">The immutable effective payload produced before admission under <paramref name="preprocessing"/>.</param>
    /// <param name="preprocessing">The non-null versioned evidence that relates the original and effective payloads.</param>
    /// <param name="admittedAt">The timestamp captured when admission was accepted.</param>
    /// <param name="promotedSequence">The committed promotion marker, or <see langword="null"/> while this input remains pending.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, the admission sequence is zero, or a promotion sequence does not follow admission.</exception>
    /// <exception cref="ArgumentException">Original and effective payload identity, delivery, or preprocessing evidence is inconsistent.</exception>
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

    /// <summary>Gets the durable identity assigned to this admission.</summary>
    /// <value>A non-default identity used to select and consume the exact admitted input once.</value>
    public AdmissionId AdmissionId { get; }
    /// <summary>Gets the agent definition addressed by this admitted input.</summary>
    /// <value>A non-default agent identity retained with the durable record.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session that owns this admission.</summary>
    /// <value>A non-default session identity used with <see cref="ExecutionLaneId"/> during promotion revalidation.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the execution lane resolved and persisted at admission.</summary>
    /// <value>A non-default immutable lane identity; promotion cannot redirect the input to another lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the execution identity captured at admission.</summary>
    /// <value>A non-null immutable identity snapshot, preserved instead of substituting an ambient or reauthenticated principal during promotion.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the durable sequence that orders this input among admitted work.</summary>
    /// <value>A positive session sequence considered by an inclusive promotion cutoff.</value>
    public SessionSequence AdmittedSequence { get; }
    /// <summary>Gets the original immutable caller payload.</summary>
    /// <value>The payload retained for idempotency comparison and provenance; it is not replaced by preprocessing.</value>
    public AgentInput OriginalPayload { get; }
    /// <summary>Gets the immutable effective payload accepted after preprocessing.</summary>
    /// <value>A payload correlated to <see cref="OriginalPayload"/> by <see cref="Preprocessing"/> and used for later promotion.</value>
    public AgentInput EffectivePayload { get; }
    /// <summary>Gets the preprocessing evidence captured before admission.</summary>
    /// <value>A non-null manifest that preserves the applied version and fingerprints for idempotent replay.</value>
    public InputPreprocessingManifest Preprocessing { get; }
    /// <summary>Gets the timestamp captured when the input was admitted.</summary>
    /// <value>The immutable admission-time observation, not a promotion or processing timestamp.</value>
    public DateTimeOffset AdmittedAt { get; }
    /// <summary>Gets the sequence of the committed promotion that consumed this input, when present.</summary>
    /// <value>A sequence greater than <see cref="AdmittedSequence"/>, or <see langword="null"/> while the input remains eligible for a future safe boundary.</value>
    public SessionSequence? PromotedSequence { get; }
}
