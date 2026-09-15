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
    private readonly double _minimumReductionRatio;
    private readonly int _targetInputTokens;
    private readonly int _minimumRetainedEntries;
    private readonly DateTimeOffset _requestedAt;
    private readonly DateTimeOffset _deadline;

    /// <summary>Initializes a new instance of the <see cref="CompactionRequest"/> record.</summary>
    /// <param name="context">The operation context for this attempt.</param>
    /// <param name="branchId">The branch to compact.</param>
    /// <param name="sourceVersion">The branch version this request was computed against.</param>
    /// <param name="sourceThrough">The last sequence eligible to be covered by this attempt.</param>
    /// <param name="contextEpoch">The context epoch this request was computed under.</param>
    /// <param name="trigger">Why this attempt was requested.</param>
    /// <param name="targetInputTokens">
    /// The advisory token budget the resulting checkpoint plus retained suffix should fit within; zero means no
    /// target. The first-party validator does not reject a candidate for exceeding it.
    /// </param>
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
    /// <paramref name="minimumRetainedEntries"/> is negative,
    /// <paramref name="minimumReductionRatio"/> is not in the open interval
    /// (0, 1), or <paramref name="deadline"/> does not follow
    /// <paramref name="requestedAt"/>.
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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            minimumReductionRatio, 0d, nameof(minimumReductionRatio));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            minimumReductionRatio, 1d, nameof(minimumReductionRatio));

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, requestedAt);

        Context = context;
        BranchId = branchId;
        SourceVersion = sourceVersion;
        SourceThrough = sourceThrough;
        ContextEpoch = contextEpoch;
        Trigger = trigger;
        _targetInputTokens = targetInputTokens;
        _minimumReductionRatio = minimumReductionRatio;
        _minimumRetainedEntries = minimumRetainedEntries;
        _requestedAt = requestedAt;
        _deadline = deadline;
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
    /// Gets the advisory token budget the resulting checkpoint plus retained
    /// suffix should fit within.
    /// </summary>
    /// <value>
    /// A nonnegative token count; zero means no target. The value informs
    /// strategies and callers but the first-party validator does not reject a
    /// candidate for exceeding it, because measurable reduction is governed by
    /// <see cref="MinimumReductionRatio"/> and the retained suffix is outside
    /// the compactor's control.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set a negative value.</exception>
    public int TargetInputTokens
    {
        get => _targetInputTokens;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(TargetInputTokens));
            _targetInputTokens = value;
        }
    }

    /// <summary>
    /// Gets the minimum fraction the candidate's estimated size must shrink
    /// by for the attempt to count as a measurable reduction.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the value outside the open interval
    /// (0, 1), including either infinity or <see cref="double.NaN"/>.
    /// </exception>
    public double MinimumReductionRatio
    {
        get => _minimumReductionRatio;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0d, nameof(MinimumReductionRatio));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, 1d, nameof(MinimumReductionRatio));
            _minimumReductionRatio = value;
        }
    }

    /// <summary>Gets the minimum number of entries that must remain in the retained suffix.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set a negative value.</exception>
    public int MinimumRetainedEntries
    {
        get => _minimumRetainedEntries;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(MinimumRetainedEntries));
            _minimumRetainedEntries = value;
        }
    }

    /// <summary>Gets the time this request was created.</summary>
    /// <remarks>
    /// An initializer that moves this value must keep it strictly before <see cref="Deadline"/>. When both values
    /// change in one <c>with</c> expression, set <see cref="Deadline"/> first or construct a new request, because
    /// initializers run in textual order and each is validated against the value the other currently holds.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set a value at or after <see cref="Deadline"/>.</exception>
    public DateTimeOffset RequestedAt
    {
        get => _requestedAt;
        init
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _deadline, nameof(RequestedAt));
            _requestedAt = value;
        }
    }

    /// <summary>Gets the instant by which this attempt must complete.</summary>
    /// <remarks>
    /// The first-party compactor compares this value with its injected <see cref="TimeProvider"/> before any
    /// session read and rejects an already-expired request with
    /// <see cref="CompactionRejectionKind.DeadlineExceeded"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set a value at or before <see cref="RequestedAt"/>.</exception>
    public DateTimeOffset Deadline
    {
        get => _deadline;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, _requestedAt, nameof(Deadline));
            _deadline = value;
        }
    }

    /// <summary>Gets caller-specific or forward-compatible request data.</summary>
    public ExtensionData Extensions { get; init; }
}
