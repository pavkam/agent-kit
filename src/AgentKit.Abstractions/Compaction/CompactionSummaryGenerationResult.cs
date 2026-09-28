// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcome of one summary generation attempt.</summary>
public abstract record CompactionSummaryGenerationResult;

/// <summary>A summary was generated successfully.</summary>
/// <param name="Summary">The generated summary.</param>
public sealed record CompactionSummaryGenerated(CompactionGeneratedSummary Summary): CompactionSummaryGenerationResult;

/// <summary>The generator declined the request under policy.</summary>
/// <param name="Rejection">The rejection outcome.</param>
public sealed record CompactionSummaryGenerationUnsupported(CompactionRejection Rejection): CompactionSummaryGenerationResult;

/// <summary>The generator failed unexpectedly.</summary>
/// <param name="Failure">The failure outcome.</param>
public sealed record CompactionSummaryGenerationFailed(CompactionFailure Failure): CompactionSummaryGenerationResult;

/// <summary>The generator attempt was cancelled.</summary>
/// <param name="Cancellation">The cancellation outcome.</param>
public sealed record CompactionSummaryGenerationCancelled(CompactionCancellation Cancellation): CompactionSummaryGenerationResult;
