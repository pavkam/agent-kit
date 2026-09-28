// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Base type for immutable compaction observation events.</summary>
public abstract record CompactionEvent
{
    /// <summary>Initializes a new instance of the <see cref="CompactionEvent"/> record.</summary>
    /// <param name="context">The operation context.</param>
    /// <param name="occurredAt">When the event occurred.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    protected CompactionEvent(CompactionOperationContext context, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the operation context.</summary>
    public CompactionOperationContext Context { get; }

    /// <summary>Gets when the event occurred.</summary>
    public DateTimeOffset OccurredAt { get; }
}

/// <summary>Published when a compaction attempt starts.</summary>
/// <param name="Context">The operation context.</param>
/// <param name="OccurredAt">When the event occurred.</param>
/// <param name="Trigger">Why the attempt was requested.</param>
public sealed record CompactionAttemptStartedEvent(
    CompactionOperationContext Context,
    DateTimeOffset OccurredAt,
    CompactionTrigger Trigger): CompactionEvent(Context, OccurredAt);

/// <summary>Published when a candidate passes validation.</summary>
/// <param name="Context">The operation context.</param>
/// <param name="OccurredAt">When the event occurred.</param>
/// <param name="Manifest">The validated manifest.</param>
public sealed record CompactionCandidateValidatedEvent(
    CompactionOperationContext Context,
    DateTimeOffset OccurredAt,
    CompactionManifest Manifest): CompactionEvent(Context, OccurredAt);

/// <summary>Published when a compaction attempt reaches a terminal outcome.</summary>
/// <param name="Context">The operation context.</param>
/// <param name="OccurredAt">When the event occurred.</param>
/// <param name="Outcome">The terminal outcome summary.</param>
public sealed record CompactionAttemptFinishedEvent(
    CompactionOperationContext Context,
    DateTimeOffset OccurredAt,
    CompactionOutcomeSummary Outcome): CompactionEvent(Context, OccurredAt);
