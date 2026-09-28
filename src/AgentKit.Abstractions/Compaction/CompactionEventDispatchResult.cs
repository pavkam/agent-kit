// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcome of dispatching one compaction event to sinks.</summary>
public abstract record CompactionEventDispatchResult;

/// <summary>Every configured sink accepted the event.</summary>
public sealed record CompactionEventPublished: CompactionEventDispatchResult;

/// <summary>A required sink could not accept the event.</summary>
public sealed record RequiredCompactionEventUnavailable(
    CompactionEventSinkId SinkId,
    CompactionFailure Failure): CompactionEventDispatchResult;
