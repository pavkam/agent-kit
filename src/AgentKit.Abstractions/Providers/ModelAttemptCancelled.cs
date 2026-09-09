// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An attempt cancelled before completion, carrying the same cancellation
/// and bounded partial output delivered by the attempt's terminal
/// <see cref="ModelResponseCancelled"/> event.
/// </summary>
public sealed record ModelAttemptCancelled: ModelAttemptResult
{
    /// <summary>Initializes a new instance of the <see cref="ModelAttemptCancelled"/> record.</summary>
    /// <param name="cancellation">The normalized cancellation failure.</param>
    /// <param name="partialParts">The bounded, uncommitted partial content produced before cancellation, if any.</param>
    /// <param name="usage">The last usage evidence before cancellation, retaining its interim or final provider report state when present.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cancellation"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="partialParts"/> is a default, uninitialized array,
    /// or <paramref name="cancellation"/>'s <see cref="ProviderFailure.Kind"/>
    /// is not <see cref="ProviderFailureKind.Cancellation"/>.
    /// </exception>
    public ModelAttemptCancelled(
        ProviderFailure cancellation,
        ImmutableArray<ContentPart> partialParts,
        ModelUsage? usage)
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

    /// <summary>Gets the last usage evidence before cancellation without changing its provider report state.</summary>
    /// <value>Captured usage evidence, or null when no report was retained; absence never means reported zero.</value>
    public ModelUsage? Usage { get; init; }

    /// <inheritdoc/>
    public bool Equals(ModelAttemptCancelled? other) =>
        other is not null
        && Cancellation.Equals(other.Cancellation)
        && PartialParts.SequenceEqual(other.PartialParts)
        && Equals(Usage, other.Usage);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Cancellation);
        foreach (var part in PartialParts)
        {
            hash.Add(part);
        }

        hash.Add(Usage);
        return hash.ToHashCode();
    }
}
