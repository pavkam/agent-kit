// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The operation may be safely attempted again, either because it is
/// idempotent or because the effect owner honors its idempotency key.
/// </summary>
/// <remarks>
/// A retry decision is only valid when repetition cannot duplicate an effect.
/// It is never justified by a durable backend's retry configuration alone: a
/// workflow engine willing to retry does not make a non-idempotent effect
/// safe to repeat.
/// </remarks>
public sealed record RecoveryRetryOperation: RecoveryDecision
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RecoveryRetryOperation"/> record.
    /// </summary>
    /// <param name="notBefore">
    /// The instant before which the retry must not be attempted, or
    /// <see langword="null"/> to retry immediately. Produced from the
    /// injected <see cref="TimeProvider"/> rather than a wall-clock delay.
    /// </param>
    /// <param name="externalIdempotencyKey">
    /// The key the effect owner accepts for collapsing duplicates, when the
    /// safety of this retry depends on one.
    /// </param>
    public RecoveryRetryOperation(
        DateTimeOffset? notBefore = null,
        IdempotencyKey? externalIdempotencyKey = null)
    {
        NotBefore = notBefore;
        ExternalIdempotencyKey = externalIdempotencyKey;
    }

    /// <summary>
    /// Gets the instant before which the retry must not be attempted, or
    /// <see langword="null"/> to retry immediately.
    /// </summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>
    /// Gets the key the effect owner accepts for collapsing duplicate
    /// attempts, or <see langword="null"/> when the operation is naturally
    /// idempotent.
    /// </summary>
    public IdempotencyKey? ExternalIdempotencyKey { get; init; }
}
