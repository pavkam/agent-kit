// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records actual normalization transformations and only measurements the normalizer obtained.</summary>
/// <remarks>This terminal evidence is distinct from later <c>ToolResultProjectionInfo</c> loss evidence.</remarks>
public sealed record ToolResultNormalizationInfo
{
    /// <summary>Initializes actual normalization evidence.</summary>
    /// <param name="transformations">Initialized unique defined transformations in application order.</param>
    /// <param name="inputCanonicalBytes">Measured input bytes, if known.</param>
    /// <param name="inputParts">Measured input parts, if known.</param>
    /// <param name="omittedCanonicalBytes">Measured omitted bytes, if known.</param>
    /// <param name="omittedParts">Measured omitted parts, if known.</param>
    /// <param name="extensions">Compatible immutable evidence.</param>
    /// <exception cref="ArgumentException"><paramref name="transformations"/> is default or duplicates a value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A transformation is undefined, a count is negative, or an omitted count exceeds its measured input count.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ToolResultNormalizationInfo(
        ImmutableArray<ToolResultNormalizationTransformation> transformations,
        long? inputCanonicalBytes,
        int? inputParts,
        long? omittedCanonicalBytes,
        int? omittedParts,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefault(transformations);
        ArgumentNullException.ThrowIfNull(extensions);
        var seen = new HashSet<ToolResultNormalizationTransformation>();
        foreach (var transformation in transformations)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(transformation, nameof(transformations));
            ArgumentException.ThrowIfNotEqual(seen.Add(transformation), true, nameof(transformations));
        }

        if (inputCanonicalBytes.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(inputCanonicalBytes.Value, nameof(inputCanonicalBytes));
        }
        if (inputParts.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(inputParts.Value, nameof(inputParts));
        }
        if (omittedCanonicalBytes.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(omittedCanonicalBytes.Value, nameof(omittedCanonicalBytes));
            if (inputCanonicalBytes.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(omittedCanonicalBytes.Value, inputCanonicalBytes.Value, nameof(omittedCanonicalBytes));
            }
        }
        if (omittedParts.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(omittedParts.Value, nameof(omittedParts));
            if (inputParts.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(omittedParts.Value, inputParts.Value, nameof(omittedParts));
            }
        }

        Transformations = transformations;
        InputCanonicalBytes = inputCanonicalBytes;
        InputParts = inputParts;
        OmittedCanonicalBytes = omittedCanonicalBytes;
        OmittedParts = omittedParts;
        Extensions = extensions;
    }

    /// <summary>Gets actual transformations in order.</summary><value>An initialized unique array.</value>
    public ImmutableArray<ToolResultNormalizationTransformation> Transformations { get; }
    /// <summary>Gets measured input bytes.</summary><value>Null when not measured.</value>
    public long? InputCanonicalBytes { get; }
    /// <summary>Gets measured input parts.</summary><value>Null when not measured.</value>
    public int? InputParts { get; }
    /// <summary>Gets measured omitted bytes.</summary><value>Null when not measured.</value>
    public long? OmittedCanonicalBytes { get; }
    /// <summary>Gets measured omitted parts.</summary><value>Null when not measured.</value>
    public int? OmittedParts { get; }
    /// <summary>Gets compatible evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }

    /// <summary>Determines structural normalization-evidence equality.</summary>
    /// <param name="other">The evidence to compare, or null.</param>
    /// <returns>True when ordered transformations, measurements, and extensions match.</returns>
    public bool Equals(ToolResultNormalizationInfo? other) => other is not null &&
        Transformations.SequenceEqual(other.Transformations) && InputCanonicalBytes == other.InputCanonicalBytes &&
        InputParts == other.InputParts && OmittedCanonicalBytes == other.OmittedCanonicalBytes &&
        OmittedParts == other.OmittedParts && Extensions == other.Extensions;

    /// <summary>Returns a hash compatible with structural equality.</summary>
    /// <returns>A hash over ordered transformations, measurements, and extensions.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Transformations)
        {
            hash.Add(item);
        }
        hash.Add(InputCanonicalBytes);
        hash.Add(InputParts);
        hash.Add(OmittedCanonicalBytes);
        hash.Add(OmittedParts);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
