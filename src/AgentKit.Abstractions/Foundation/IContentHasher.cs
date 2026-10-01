// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Computes self-describing <see cref="ContentHash"/> values over already-canonicalized content.</summary>
/// <remarks>
/// <para>
/// A semantic owner canonicalizes its value first and names the canonicalization it applied; the hasher never guesses
/// a JSON, message, configuration, or store serialization. The returned <see cref="ContentHash"/> text records the
/// algorithm, algorithm version, canonicalization, and canonicalization version ahead of the lowercase-hex digest, so
/// changing any of them changes equality explicitly rather than silently.
/// </para>
/// <para>
/// Implementations are thread-safe immutable singletons. The first-party default is SHA-256.
/// </para>
/// </remarks>
public interface IContentHasher
{
    /// <summary>Gets the algorithm this hasher implements.</summary>
    public ContentHashAlgorithmId Algorithm { get; }

    /// <summary>Gets the version of <see cref="Algorithm"/> this hasher implements.</summary>
    public ContentHashAlgorithmVersion AlgorithmVersion { get; }

    /// <summary>Hashes canonical content held in memory.</summary>
    /// <param name="canonicalContent">The already-canonicalized bytes; an empty span is valid.</param>
    /// <param name="canonicalization">The canonicalization the owner applied.</param>
    /// <param name="canonicalizationVersion">The version of that canonicalization.</param>
    /// <returns>The self-describing hash.</returns>
    /// <exception cref="ArgumentException"><paramref name="canonicalization"/> or <paramref name="canonicalizationVersion"/> is the default value.</exception>
    public ContentHash Compute(
        ReadOnlySpan<byte> canonicalContent,
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion);

    /// <summary>Hashes canonical content read from a stream without buffering all of it.</summary>
    /// <param name="canonicalContent">The readable stream positioned at the first canonical byte; it is read to its end and not disposed.</param>
    /// <param name="canonicalization">The canonicalization the owner applied.</param>
    /// <param name="canonicalizationVersion">The version of that canonicalization.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The self-describing hash.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="canonicalContent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="canonicalization"/> or <paramref name="canonicalizationVersion"/> is the default value.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<ContentHash> ComputeAsync(
        Stream canonicalContent,
        CanonicalizationProfileId canonicalization,
        CanonicalizationProfileVersion canonicalizationVersion,
        CancellationToken cancellationToken = default);
}
