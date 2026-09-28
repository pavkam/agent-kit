// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcome of one compaction activation attempt.</summary>
public abstract record CompactionActivationResult;

/// <summary>The validated record was activated durably.</summary>
/// <param name="Record">The committed compaction record.</param>
public sealed record CompactionRecordActivated(CompactionRecord Record): CompactionActivationResult;

/// <summary>The append conflicted with a concurrent branch advance.</summary>
public sealed record CompactionRecordConflict(
    SessionVersion ExpectedVersion,
    SessionVersion ActualVersion,
    CompactionManifest Manifest): CompactionActivationResult;

/// <summary>Activation was rejected before append.</summary>
/// <param name="Rejection">The rejection outcome.</param>
public sealed record CompactionRecordActivationRejected(CompactionRejection Rejection): CompactionActivationResult;

/// <summary>Activation failed unexpectedly.</summary>
/// <param name="Failure">The failure outcome.</param>
/// <param name="ActivatedVersionClaim">
/// When the store committed an append but reported a version other than the record claims, the version the record
/// claims; otherwise <see langword="null"/>.
/// </param>
/// <param name="StoreReportedVersion">
/// When the store committed an append but reported a version other than the record claims, the version the store
/// reported; otherwise <see langword="null"/>.
/// </param>
public sealed record CompactionRecordActivationFailed(
    CompactionFailure Failure,
    SessionVersion? ActivatedVersionClaim = null,
    SessionVersion? StoreReportedVersion = null): CompactionActivationResult;

/// <summary>Activation was cancelled before a terminal outcome was established.</summary>
/// <param name="Cancellation">The cancellation outcome.</param>
public sealed record CompactionRecordActivationCancelled(CompactionCancellation Cancellation): CompactionActivationResult;
