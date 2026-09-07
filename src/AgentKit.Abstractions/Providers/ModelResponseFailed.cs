// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The terminal event of one failed attempt. No further events follow for
/// the same request.
/// </summary>
/// <remarks>
/// <see cref="PartialParts"/> and <see cref="Usage"/> preserve whatever
/// bounded partial output the attempt produced before it failed; they are
/// diagnostic evidence only and are never presented as a committed
/// <see cref="ModelResponse"/>.
/// </remarks>
public sealed record ModelResponseFailed: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelResponseFailed"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    /// <param name="failure">The normalized failure.</param>
    /// <param name="partialParts">The bounded, uncommitted partial content produced before the failure, if any.</param>
    /// <param name="usage">The last known usage before the failure, if any was reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="partialParts"/> is a default, uninitialized array.
    /// </exception>
    public ModelResponseFailed(
        ModelRequestId requestId,
        long sequence,
        ProviderFailure failure,
        ImmutableArray<ContentPart> partialParts,
        ModelUsage? usage)
        : base(requestId, sequence)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentException.ThrowIfDefault(partialParts);

        Failure = failure;
        PartialParts = partialParts;
        Usage = usage;
    }

    /// <summary>Gets the normalized failure.</summary>
    public ProviderFailure Failure { get; init; }

    /// <summary>Gets the bounded, uncommitted partial content produced before the failure, if any.</summary>
    public ImmutableArray<ContentPart> PartialParts { get; init; }

    /// <summary>Gets the last known usage before the failure, if any was reported.</summary>
    public ModelUsage? Usage { get; init; }

    /// <inheritdoc/>
    public bool Equals(ModelResponseFailed? other) =>
        other is not null
        && RequestId.Equals(other.RequestId)
        && Sequence == other.Sequence
        && Failure.Equals(other.Failure)
        && PartialParts.SequenceEqual(other.PartialParts)
        && Equals(Usage, other.Usage);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RequestId);
        hash.Add(Sequence);
        hash.Add(Failure);
        foreach (var part in PartialParts)
        {
            hash.Add(part);
        }

        hash.Add(Usage);
        return hash.ToHashCode();
    }
}
