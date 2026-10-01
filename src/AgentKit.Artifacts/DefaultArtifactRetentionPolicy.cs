// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Resolves retention conservatively and refuses deletion of held or externally governed content.</summary>
/// <remarks>
/// <para>
/// Resolution rejects a request whose expiry already passed, keeps the strictest legal hold between the request and the profile
/// default, and inherits the profile default's expiry only when the request names the same policy and sets none. A retention expiry
/// marks when content becomes eligible for expiry collection; it is not a minimum retention period, so an authorized owner may delete
/// earlier.
/// </para>
/// <para>Deletion is refused under legal hold and when the external owner did not delegate delete authority. The policy is stateless and thread-safe.</para>
/// </remarks>
public sealed class DefaultArtifactRetentionPolicy: IArtifactRetentionPolicy
{
    /// <inheritdoc/>
    public ValueTask<ArtifactRetentionDecision> ResolveAsync(ArtifactRetentionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var requested = request.Metadata.Retention;
        var profileDefault = request.ProfileDefault;
        if (requested.ExpiresAt is { } expiry && expiry <= request.Now)
        {
            return Decision(new ArtifactRetentionRejected(
                new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "The requested retention has already expired.")));
        }

        var expiresAt = requested.ExpiresAt
            ?? (requested.Policy == profileDefault.Policy ? profileDefault.ExpiresAt : null);
        var resolved = new ArtifactRetention(requested.Policy, expiresAt, requested.LegalHold || profileDefault.LegalHold);
        return Decision(new ArtifactRetentionAllowed(resolved));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactRetentionDecision> EvaluateDeletionAsync(ArtifactReference reference, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        _ = now;
        return Decision(
            reference.Retention.LegalHold
                ? new ArtifactRetentionRejected(new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "Artifact retention prohibits deletion."))
                : reference.ExternalOwnership is { AgentKitMayDelete: false }
                    ? new ArtifactRetentionRejected(new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "The external owner retains deletion authority."))
                    : new ArtifactRetentionAllowed(reference.Retention));
    }

    private static ValueTask<ArtifactRetentionDecision> Decision(ArtifactRetentionDecision decision) => ValueTask.FromResult(decision);
}
