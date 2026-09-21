// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of one history-preparation pass.</summary>
public abstract record HistoryPreparationResult;

/// <summary>History preparation produced a bounded, validated view.</summary>
/// <param name="View">The repaired history view at the requested cursor.</param>
public sealed record PreparedHistory(HistoryView View)
    : HistoryPreparationResult;

/// <summary>History preparation failed before a provider-ready view could be produced.</summary>
/// <param name="Failure">The typed failure describing why preparation stopped.</param>
public sealed record RejectedHistory(HistoryFailure Failure)
    : HistoryPreparationResult;
