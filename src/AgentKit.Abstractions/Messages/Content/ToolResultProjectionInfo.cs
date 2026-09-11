// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains the exact policy reference and measured loss evidence for one tool-result projection.</summary>
/// <remarks>
/// This immutable value is safe to share across threads. It describes projection after terminal recording,
/// separately from terminal-result normalization. It does not resolve policy content, authorize transformations,
/// measure content, or prove that a projector enforced its captured bounds. Publication retries must use
/// <see cref="Policy"/> rather than substitute a current policy version.
/// </remarks>
public sealed record ToolResultProjectionInfo
{
    /// <summary>Initializes validated projection provenance without looking up or replacing its captured policy.</summary>
    /// <param name="policy">The nonnull policy key and exact version captured with the authoritative terminal result.</param>
    /// <param name="losses">The initialized, ordered projection losses. Repeated losses preserve repeated transformation evidence; an empty collection declares no loss.</param>
    /// <param name="omittedBytes">The measured nonnegative number of source-content bytes omitted from this projection; zero declares that none were omitted.</param>
    /// <param name="omittedParts">The measured nonnegative number of source-content parts omitted from this projection; zero declares that none were omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An omitted count is negative or <paramref name="losses"/> contains an undefined value.</exception>
    /// <exception cref="ArgumentException"><paramref name="losses"/> is uninitialized, or a positive omitted count has no content-loss marker.</exception>
    /// <remarks>
    /// Every loss except <see cref="ToolResultProjectionLoss.StatusCoarsened"/> describes a content transformation.
    /// A content transformation may retain the original byte and part counts, so its presence does not require
    /// a positive omitted count. Status coarsening alone cannot account for omitted content. Counts describe
    /// this projection and do not include earlier terminal-normalization omissions.
    /// </remarks>
    public ToolResultProjectionInfo(
        ToolResultProjectionPolicyReference policy,
        ImmutableArray<ToolResultProjectionLoss> losses,
        long omittedBytes,
        int omittedParts)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfDefault(losses);
        ArgumentOutOfRangeException.ThrowIfNegative(omittedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(omittedParts);
        foreach (var loss in losses)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(loss, nameof(losses));
        }

        ArgumentException.ThrowIfNotEqual(
            (omittedBytes == 0 && omittedParts == 0) || losses.Any(static loss => loss != ToolResultProjectionLoss.StatusCoarsened),
            true,
            nameof(losses));

        Policy = policy;
        Losses = losses;
        OmittedBytes = omittedBytes;
        OmittedParts = omittedParts;
    }

    /// <summary>Gets the exact projection-policy reference retained by the authoritative terminal result.</summary>
    /// <value>The immutable key and positive version; retaining it grants no authority and does not imply that its snapshot is available.</value>
    public ToolResultProjectionPolicyReference Policy { get; }

    /// <summary>Gets the ordered losses introduced by this projection.</summary>
    /// <value>An initialized immutable sequence of defined loss values, including repeated transformations in their original order.</value>
    public ImmutableArray<ToolResultProjectionLoss> Losses { get; }

    /// <summary>Gets the measured source-content bytes omitted by this projection.</summary>
    /// <value>A nonnegative count; any positive value requires content-loss evidence in <see cref="Losses"/>.</value>
    public long OmittedBytes { get; }

    /// <summary>Gets the measured source-content parts omitted by this projection.</summary>
    /// <value>A nonnegative count; any positive value requires content-loss evidence in <see cref="Losses"/>.</value>
    public int OmittedParts { get; }

    /// <summary>Compares captured policy, ordered loss evidence, and both measured counts.</summary>
    /// <param name="other">The provenance to compare, or null.</param>
    /// <returns>True only when every field matches, including loss order and repetition, independently of array storage identity.</returns>
    public bool Equals(ToolResultProjectionInfo? other) =>
        other is not null && Policy == other.Policy && Losses.SequenceEqual(other.Losses)
        && OmittedBytes == other.OmittedBytes && OmittedParts == other.OmittedParts;

    /// <summary>Computes a structural hash consistent with complete projection-provenance equality.</summary>
    /// <returns>A hash of the captured policy, ordered losses, and omitted counts.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Policy);
        foreach (var loss in Losses)
        {
            hash.Add(loss);
        }

        hash.Add(OmittedBytes);
        hash.Add(OmittedParts);
        return hash.ToHashCode();
    }
}
