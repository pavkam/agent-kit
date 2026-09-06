// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request to compact a bounded prefix of a session
/// branch into a checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>CompactionRequest</c> described by the context-compaction
/// architecture, which additionally carries a full versioned
/// <c>CompactionPolicySnapshot</c> (profile key/version, compactor key,
/// ordered strategy list, and several more engine ceilings) and an
/// effective-instructions fingerprint. This shape folds the two policy
/// values a first-party extractive strategy actually needs —
/// <see cref="MinimumReductionRatio"/> and <see cref="MinimumRetainedEntries"/> —
/// directly onto the request; once a dedicated policy/profile system
/// exists, requests will carry that richer snapshot instead.
/// </para>
/// </remarks>
public sealed record CompactionRequest
{
    /// <summary>Initializes a new instance of the <see cref="CompactionRequest"/> record.</summary>
    /// <param name="context">The operation context for this attempt.</param>
    /// <param name="branchId">The branch to compact.</param>
    /// <param name="sourceVersion">The branch version this request was computed against.</param>
    /// <param name="sourceThrough">The last sequence eligible to be covered by this attempt.</param>
    /// <param name="contextEpoch">The context epoch this request was computed under.</param>
    /// <param name="trigger">Why this attempt was requested.</param>
    /// <param name="targetInputTokens">The token budget the resulting checkpoint plus retained suffix should fit within.</param>
    /// <param name="minimumReductionRatio">
    /// The minimum fraction (in the open interval (0, 1)) the candidate's
    /// estimated size must shrink by for the attempt to count as a
    /// measurable reduction.
    /// </param>
    /// <param name="minimumRetainedEntries">The minimum number of entries that must remain in the retained suffix.</param>
    /// <param name="requestedAt">The time this request was created.</param>
    /// <param name="deadline">The instant by which this attempt must complete.</param>
    /// <param name="extensions">Caller-specific or forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="trigger"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="targetInputTokens"/> or
    /// <paramref name="minimumRetainedEntries"/> is negative, or
    /// <paramref name="minimumReductionRatio"/> is not in the open interval
    /// (0, 1).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="deadline"/> does not follow <paramref name="requestedAt"/>.
    /// </exception>
    public CompactionRequest(
        CompactionOperationContext context,
        BranchId branchId,
        SessionVersion sourceVersion,
        SessionSequence sourceThrough,
        ContextEpoch contextEpoch,
        CompactionTrigger trigger,
        int targetInputTokens,
        double minimumReductionRatio,
        int minimumRetainedEntries,
        DateTimeOffset requestedAt,
        DateTimeOffset deadline,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentOutOfRangeException.ThrowIfNegative(targetInputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRetainedEntries);
        if (minimumReductionRatio is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumReductionRatio), minimumReductionRatio, "Value must be in the open interval (0, 1).");
        }

        if (deadline <= requestedAt)
        {
            throw new ArgumentException("Deadline must follow RequestedAt.", nameof(deadline));
        }

        Context = context;
        BranchId = branchId;
        SourceVersion = sourceVersion;
        SourceThrough = sourceThrough;
        ContextEpoch = contextEpoch;
        Trigger = trigger;
        TargetInputTokens = targetInputTokens;
        MinimumReductionRatio = minimumReductionRatio;
        MinimumRetainedEntries = minimumRetainedEntries;
        RequestedAt = requestedAt;
        Deadline = deadline;
        Extensions = extensions;
    }

    /// <summary>Gets the operation context for this attempt.</summary>
    public CompactionOperationContext Context { get; init; }

    /// <summary>Gets the branch to compact.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the branch version this request was computed against.</summary>
    public SessionVersion SourceVersion { get; init; }

    /// <summary>Gets the last sequence eligible to be covered by this attempt.</summary>
    public SessionSequence SourceThrough { get; init; }

    /// <summary>Gets the context epoch this request was computed under.</summary>
    public ContextEpoch ContextEpoch { get; init; }

    /// <summary>Gets why this attempt was requested.</summary>
    public CompactionTrigger Trigger { get; init; }

    /// <summary>
    /// Gets the token budget the resulting checkpoint plus retained suffix
    /// should fit within.
    /// </summary>
    public int TargetInputTokens { get; init; }

    /// <summary>
    /// Gets the minimum fraction the candidate's estimated size must shrink
    /// by for the attempt to count as a measurable reduction.
    /// </summary>
    public double MinimumReductionRatio { get; init; }

    /// <summary>Gets the minimum number of entries that must remain in the retained suffix.</summary>
    public int MinimumRetainedEntries { get; init; }

    /// <summary>Gets the time this request was created.</summary>
    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>Gets the instant by which this attempt must complete.</summary>
    public DateTimeOffset Deadline { get; init; }

    /// <summary>Gets caller-specific or forward-compatible request data.</summary>
    public ExtensionData Extensions { get; init; }
}
