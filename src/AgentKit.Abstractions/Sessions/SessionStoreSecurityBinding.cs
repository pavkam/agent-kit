// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Produces canonical resource and request fingerprints for protected session-store effects.</summary>
/// <remarks>Bindings hash the complete immutable request graph, including content and extension bytes, but expose only the digest. Canonical encoding is length-prefixed, type-tagged, culture invariant, property-order stable, and dictionary-order independent.</remarks>
public static class SessionStoreSecurityBinding
{
    /// <summary>Fingerprints one canonical protected resource for redacted audit retention.</summary><param name="resource">The complete resource value.</param><returns>An algorithm-qualified digest that does not expose the resource text.</returns><exception cref="ArgumentNullException"><paramref name="resource"/> is null.</exception>
    public static ContentHash FingerprintResource(ProtectedResource resource) =>
        new(SecurityCanonicalFingerprint.Create(resource).Value);

    /// <summary>Creates the canonical protected resource for one session.</summary>
    /// <param name="storeKey">The selected store.</param>
    /// <param name="address">The session address.</param>
    /// <returns>A content-free application-state resource.</returns>
    public static ProtectedResource Resource(SessionStoreKey storeKey, SessionAddress address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey.Value);
        ArgumentNullException.ThrowIfNull(address);
        return new ProtectedResource(
            ProtectedResourceKind.ApplicationState,
            $"session:{storeKey.Value}:{address.AgentId}:{address.SessionId}");
    }

    /// <summary>Creates the canonical protected resource for session creation before a session identity exists.</summary>
    /// <param name="storeKey">The selected store.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <returns>A content-free application-state resource.</returns>
    public static ProtectedResource CreationResource(SessionStoreKey storeKey, AgentId agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey.Value);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        return new ProtectedResource(
            ProtectedResourceKind.ApplicationState,
            $"session-create:{storeKey.Value}:{agentId}");
    }

    /// <summary>Fingerprints one exact session creation request.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionStoreCreateRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact session load context.</summary><param name="request">The complete immutable context.</param><returns>An algorithm-qualified digest over all context evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionOperationContext request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact lane-provision transaction, including branch, profile, configuration, time, and authority evidence.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionExecutionLaneProvisionRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact append request, including every entry and content byte.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionAppendRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact page-read request.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionReadRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact branch-creation request.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionBranchRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact session-deletion request.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionDeleteRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact admitted-input lookup, including original payload and captured authority.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionInputLookupRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact input admission, including original and effective payloads and preprocessing evidence.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionInputAdmissionRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact atomic run-acceptance proposal, including retained recovery and authority evidence.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionRunStartRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact accepted-run-state load request.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionRunStateRequest request) => SecurityCanonicalFingerprint.Create(request);

    /// <summary>Fingerprints one exact lane-release request, including the expected state revision and session version.</summary><param name="request">The complete immutable request.</param><returns>An algorithm-qualified digest over all request evidence.</returns><exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(SessionRunReleaseRequest request) => SecurityCanonicalFingerprint.Create(request);
}
