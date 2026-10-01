// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>The default <see cref="IContentHasher"/>: SHA-256 over canonical bytes.</summary>
/// <remarks>
/// The hasher is stateless and thread-safe. The produced <see cref="ContentHash"/> text is
/// <c>sha256/1/{canonicalization}/{canonicalizationVersion}:{lowercase hex}</c>, so the algorithm and canonicalization
/// are part of equality.
/// </remarks>
internal sealed class Sha256ContentHasher: IContentHasher
{
    /// <inheritdoc/>
    public ContentHashAlgorithmId Algorithm { get; } = new("sha256");

    /// <inheritdoc/>
    public ContentHashAlgorithmVersion AlgorithmVersion { get; } = new("1");

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="canonicalization"/> or <paramref name="canonicalizationVersion"/> is the default value.</exception>
    public ContentHash Compute(
        ReadOnlySpan<byte> canonicalContent,
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion)
    {
        Validate(canonicalization, canonicalizationVersion);
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(canonicalContent, digest);
        return Describe(digest, canonicalization, canonicalizationVersion);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="canonicalContent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="canonicalization"/> or <paramref name="canonicalizationVersion"/> is the default value.</exception>
    public async ValueTask<ContentHash> ComputeAsync(
        Stream canonicalContent,
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canonicalContent);
        Validate(canonicalization, canonicalizationVersion);
        var digest = await SHA256.HashDataAsync(canonicalContent, cancellationToken).ConfigureAwait(false);
        return Describe(digest, canonicalization, canonicalizationVersion);
    }

    private static void Validate(
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalization.Value, nameof(canonicalization));
        ArgumentOutOfRangeException.ThrowIfEqual(canonicalizationVersion, default, nameof(canonicalizationVersion));
    }

    private ContentHash Describe(
        ReadOnlySpan<byte> digest,
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion) => new(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Algorithm.Value}/{AlgorithmVersion.Value}/{canonicalization.Value}/{canonicalizationVersion}:{Convert.ToHexStringLower(digest)}"));
}
