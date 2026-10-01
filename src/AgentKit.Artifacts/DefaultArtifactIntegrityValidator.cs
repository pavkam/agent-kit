// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Validates complete content with SHA-256 and the declared byte length.</summary>
/// <remarks>The validator is stateless and thread-safe. Hashes carry the <c>sha256:</c> prefix produced by <see cref="FileSecurityBinding.ContentFingerprint"/>, which first-party stores recompute before publishing.</remarks>
public sealed class DefaultArtifactIntegrityValidator: IArtifactIntegrityValidator
{
    /// <inheritdoc/>
    public string Algorithm => "sha256";

    /// <inheritdoc/>
    public ValueTask<ArtifactIntegrityResult> ValidateAsync(
        ReadOnlyMemory<byte> content,
        long declaredLength,
        ContentHash? declaredContentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(declaredLength);
        cancellationToken.ThrowIfCancellationRequested();
        if (content.Length != declaredLength)
        {
            return Rejected("Observed artifact length did not match its declaration.");
        }

        var observed = FileSecurityBinding.ContentFingerprint(content.Span);
        return declaredContentHash is { } declared && declared != observed
            ? Rejected("Observed artifact integrity did not match its declaration.")
            : ValueTask.FromResult<ArtifactIntegrityResult>(new ArtifactIntegrityVerified(observed));
    }

    private static ValueTask<ArtifactIntegrityResult> Rejected(string message) =>
        ValueTask.FromResult<ArtifactIntegrityResult>(
            new ArtifactIntegrityRejected(new ArtifactFailure(ArtifactFailureKind.IntegrityMismatch, message)));
}
