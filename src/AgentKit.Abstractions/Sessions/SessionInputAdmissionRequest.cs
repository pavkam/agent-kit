// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests atomic persistence of one preprocessed lane-bound input.</summary>
public sealed record SessionInputAdmissionRequest
{
    /// <summary>Initializes a durable admission request.</summary>
    /// <param name="context">The lane-bound before-run session context.</param><param name="admissionId">The new runtime-owned admission identity.</param><param name="entryId">The new session-entry identity reserved for the durable admission fact.</param><param name="originalPayload">The immutable caller payload.</param><param name="effectivePayload">The preprocessed payload.</param><param name="preprocessing">The captured preprocessing evidence.</param><param name="admittedAt">The deterministic acceptance time.</param><param name="expectedVersion">The observed canonical whole-session version.</param><param name="expectedLaneRevision">The observed lane revision.</param><param name="branchCursor">The exact observed lane-owned branch tip.</param><param name="idempotencyKey">The exact commit idempotency key.</param><param name="maximumPendingInputs">The positive per-session pending-input bound.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception><exception cref="ArgumentOutOfRangeException">An identity is default or the bound is not positive.</exception><exception cref="ArgumentException">The context or payload evidence is inconsistent.</exception>
    public SessionInputAdmissionRequest(SessionOperationContext context, AdmissionId admissionId, SessionEntryId entryId, AgentInput originalPayload,
        AgentInput effectivePayload, InputPreprocessingManifest preprocessing, DateTimeOffset admittedAt,
        SessionVersion expectedVersion, SessionLaneRevision expectedLaneRevision, SessionBranchCursor branchCursor,
        IdempotencyKey idempotencyKey, int maximumPendingInputs)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentOutOfRangeException.ThrowIfEqual(admissionId, default); ArgumentOutOfRangeException.ThrowIfEqual(entryId, default);
        ArgumentException.ThrowIfSessionContextNotLaneBound(context);
        ArgumentException.ThrowIfSessionContextNotBeforeRun(context);
        ArgumentException.ThrowIfInvalidAdmittedInputPayloads(originalPayload, effectivePayload, preprocessing);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedLaneRevision, default);
        ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPendingInputs);
        Context = context; AdmissionId = admissionId; EntryId = entryId; OriginalPayload = originalPayload; EffectivePayload = effectivePayload;
        Preprocessing = preprocessing; AdmittedAt = admittedAt; ExpectedVersion = expectedVersion;
        ExpectedLaneRevision = expectedLaneRevision; BranchCursor = branchCursor;
        IdempotencyKey = idempotencyKey; MaximumPendingInputs = maximumPendingInputs;
    }
    /// <summary>Gets the protected session context.</summary><value>A lane-bound before-run context.</value>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the proposed admission identity.</summary><value>A non-default identity used only if a new commit wins.</value>
    public AdmissionId AdmissionId { get; }
    /// <summary>Gets the admission entry identity.</summary><value>The non-default identity used only if a new commit wins.</value>
    public SessionEntryId EntryId { get; }
    /// <summary>Gets the original payload.</summary><value>The caller input retained for replay comparison.</value>
    public AgentInput OriginalPayload { get; }
    /// <summary>Gets the effective payload.</summary><value>The immutable input later materialized into history.</value>
    public AgentInput EffectivePayload { get; }
    /// <summary>Gets preprocessing evidence.</summary><value>The version and canonical input fingerprints.</value>
    public InputPreprocessingManifest Preprocessing { get; }
    /// <summary>Gets the acceptance time.</summary><value>The deterministic timestamp committed with the admission.</value>
    public DateTimeOffset AdmittedAt { get; }
    /// <summary>Gets the expected canonical session version.</summary><value>Whole-session optimistic-concurrency evidence checked after idempotency reconciliation.</value>
    public SessionVersion ExpectedVersion { get; }
    /// <summary>Gets the expected lane revision.</summary><value>Lane-local optimistic-concurrency evidence checked after idempotency reconciliation.</value>
    public SessionLaneRevision ExpectedLaneRevision { get; }
    /// <summary>Gets the expected lane-owned branch cursor.</summary><value>The exact branch tip that must still belong to this lane.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the commit idempotency key.</summary><value>The key used to reconcile ambiguous calls.</value>
    public IdempotencyKey IdempotencyKey { get; }
    /// <summary>Gets the pending-input bound.</summary><value>A positive capacity ceiling that replay never consumes.</value>
    public int MaximumPendingInputs { get; }
}
