// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates complete bounded content against its declared length and hash and computes the recorded hash.</summary>
/// <remarks>The validator is stateless and thread-safe. It never logs or retains content, and it runs before any backend effect.</remarks>
public interface IArtifactIntegrityValidator
{
    /// <summary>Gets the hash algorithm this validator declares and computes.</summary>
    /// <value>A non-blank algorithm name, such as <c>sha256</c>, that prefixes every hash it verifies.</value>
    public string Algorithm { get; }

    /// <summary>Validates complete content and computes its hash.</summary>
    /// <param name="content">The complete bounded content.</param>
    /// <param name="declaredLength">The exact declared byte length.</param>
    /// <param name="declaredContentHash">The declared hash, or <see langword="null"/> when none was declared.</param>
    /// <param name="cancellationToken">Cancels validation.</param>
    /// <returns>The observed hash, or a typed integrity mismatch.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactIntegrityResult> ValidateAsync(
        ReadOnlyMemory<byte> content,
        long declaredLength,
        ContentHash? declaredContentHash,
        CancellationToken cancellationToken = default);
}
