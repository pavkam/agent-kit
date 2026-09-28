// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The journaled manifest of one input-promotion attempt at a safe loop boundary.</summary>
/// <remarks>
/// <para>
/// This is a manifest, not a serialized transition: it identifies which promotion attempt this is and records
/// enough to detect that a replay describes different work. The promoted messages themselves are already durable
/// session truth, so no message text, part, or payload enters this record.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over its fields. It carries no mutable state and
/// is safe to share across threads without synchronization.
/// </para>
/// </remarks>
public sealed record DurableInputPromotionManifest
{
    /// <summary>Initializes one complete input-promotion manifest.</summary>
    /// <param name="runId">The nonempty identity of the run whose boundary this promotion occurs at.</param>
    /// <param name="targetTurnId">The nonempty identity of the turn that would receive the promoted input.</param>
    /// <param name="boundary">The exact safe boundary this attempt occurs at, as its defined enumeration name.</param>
    /// <param name="promotedMessageCount">
    /// The nonnegative number of messages the promotion made visible, or zero before the promotion commits.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="runId"/> or <paramref name="targetTurnId"/> is the empty identity, or
    /// <paramref name="promotedMessageCount"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="boundary"/> is null, empty, or whitespace.</exception>
    public DurableInputPromotionManifest(
        Guid runId,
        Guid targetTurnId,
        string boundary,
        int promotedMessageCount)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, Guid.Empty, nameof(runId));
        ArgumentOutOfRangeException.ThrowIfEqual(targetTurnId, Guid.Empty, nameof(targetTurnId));
        ArgumentException.ThrowIfNullOrWhiteSpace(boundary);
        ArgumentOutOfRangeException.ThrowIfNegative(promotedMessageCount);
        RunId = runId;
        TargetTurnId = targetTurnId;
        Boundary = boundary;
        PromotedMessageCount = promotedMessageCount;
    }

    /// <summary>Gets the run whose boundary this promotion occurs at.</summary>
    /// <value>The nonempty run identity, matching the durable address this operation is journaled under.</value>
    public Guid RunId { get; }

    /// <summary>Gets the turn that would receive the promoted input.</summary>
    /// <value>The nonempty turn identity the atomic history transition targets.</value>
    public Guid TargetTurnId { get; }

    /// <summary>Gets the safe boundary this attempt occurs at.</summary>
    /// <value>The nonblank defined <see cref="PromotionBoundary"/> name, which is configuration rather than content.</value>
    public string Boundary { get; }

    /// <summary>Gets how many messages the promotion made visible.</summary>
    /// <value>A nonnegative count; zero in the declaration written before the promotion is attempted.</value>
    public int PromotedMessageCount { get; }
}
