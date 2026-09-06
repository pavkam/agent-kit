// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The terminal event of one attempt cancelled before completion. No
/// further events follow for the same request.
/// </summary>
/// <remarks>
/// <see cref="Cancellation"/> always carries a <see cref="ProviderFailure"/>
/// whose <see cref="ProviderFailure.Kind"/> is
/// <see cref="ProviderFailureKind.Cancellation"/>.
/// <see cref="PartialParts"/> and <see cref="Usage"/> preserve whatever
/// bounded partial output the attempt produced before it was cancelled;
/// they are diagnostic evidence only and are never presented as a
/// committed <see cref="ModelResponse"/>.
/// </remarks>
public sealed record ModelResponseCancelled: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelResponseCancelled"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    /// <param name="cancellation">The normalized cancellation failure.</param>
    /// <param name="partialParts">The bounded, uncommitted partial content produced before cancellation, if any.</param>
    /// <param name="usage">The last known usage before cancellation, if any was reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cancellation"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="partialParts"/> is a default, uninitialized array,
    /// or <paramref name="cancellation"/>'s <see cref="ProviderFailure.Kind"/>
    /// is not <see cref="ProviderFailureKind.Cancellation"/>.
    /// </exception>
    public ModelResponseCancelled(
        ModelRequestId requestId,
        long sequence,
        ProviderFailure cancellation,
        ImmutableArray<ContentPart> partialParts,
        ModelUsage? usage)
        : base(requestId, sequence)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentException.ThrowIfDefault(partialParts);

        if (cancellation.Kind != ProviderFailureKind.Cancellation)
        {
            throw new ArgumentException(
                $"{nameof(cancellation)} must have {nameof(ProviderFailure.Kind)} equal to " +
                $"{nameof(ProviderFailureKind.Cancellation)}.",
                nameof(cancellation));
        }

        Cancellation = cancellation;
        PartialParts = partialParts;
        Usage = usage;
    }

    /// <summary>Gets the normalized cancellation failure.</summary>
    public ProviderFailure Cancellation { get; init; }

    /// <summary>Gets the bounded, uncommitted partial content produced before cancellation, if any.</summary>
    public ImmutableArray<ContentPart> PartialParts { get; init; }

    /// <summary>Gets the last known usage before cancellation, if any was reported.</summary>
    public ModelUsage? Usage { get; init; }
}
