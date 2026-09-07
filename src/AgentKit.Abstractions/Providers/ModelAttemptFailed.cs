// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An attempt that failed, carrying the same failure and bounded partial
/// output delivered by the attempt's terminal
/// <see cref="ModelResponseFailed"/> event.
/// </summary>
public sealed record ModelAttemptFailed: ModelAttemptResult
{
    /// <summary>Initializes a new instance of the <see cref="ModelAttemptFailed"/> record.</summary>
    /// <param name="failure">The normalized failure.</param>
    /// <param name="partialParts">The bounded, uncommitted partial content produced before the failure, if any.</param>
    /// <param name="usage">The last known usage before the failure, if any was reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="partialParts"/> is a default, uninitialized array.
    /// </exception>
    public ModelAttemptFailed(
        ProviderFailure failure,
        ImmutableArray<ContentPart> partialParts,
        ModelUsage? usage)
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
    public bool Equals(ModelAttemptFailed? other) =>
        other is not null
        && Failure.Equals(other.Failure)
        && PartialParts.SequenceEqual(other.PartialParts)
        && Equals(Usage, other.Usage);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Failure);
        foreach (var part in PartialParts)
        {
            hash.Add(part);
        }

        hash.Add(Usage);
        return hash.ToHashCode();
    }
}
