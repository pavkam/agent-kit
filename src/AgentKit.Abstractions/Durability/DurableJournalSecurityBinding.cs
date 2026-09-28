// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Produces canonical resource and request fingerprints for protected durable-journal effects.</summary>
public static class DurableJournalSecurityBinding
{
    /// <summary>Fingerprints one canonical protected resource for redacted audit retention.</summary>
    /// <param name="resource">The complete resource value.</param>
    /// <returns>An algorithm-qualified digest that does not expose the resource text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is null.</exception>
    public static ContentHash FingerprintResource(ProtectedResource resource) =>
        new(SecurityCanonicalFingerprint.Create(resource).Value);

    /// <summary>Creates the canonical protected resource for one durable operation address.</summary>
    /// <param name="journalKey">The selected journal.</param>
    /// <param name="address">The operation address.</param>
    /// <returns>A content-free application-state resource.</returns>
    public static ProtectedResource Resource(DurableJournalKey journalKey, DurableOperationAddress address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalKey.Value, nameof(journalKey));
        ArgumentNullException.ThrowIfNull(address);
        return new ProtectedResource(
            ProtectedResourceKind.ApplicationState,
            $"durable-journal:{journalKey.Value}:{address.AgentId}:{address.SessionId}:{address.RunId}:{address.OperationId}");
    }

    /// <summary>Fingerprints one exact durable operation start record.</summary>
    /// <param name="start">The complete immutable start request.</param>
    /// <returns>An algorithm-qualified digest over all request evidence.</returns>
    public static InputFingerprint Fingerprint(DurableOperationStart start) =>
        SecurityCanonicalFingerprint.Create(start);

    /// <summary>Fingerprints one exact durable checkpoint.</summary>
    /// <param name="checkpoint">The complete immutable checkpoint.</param>
    /// <returns>An algorithm-qualified digest over all request evidence.</returns>
    public static InputFingerprint Fingerprint(DurableCheckpoint checkpoint) =>
        SecurityCanonicalFingerprint.Create(checkpoint);

    /// <summary>Fingerprints one exact terminal durable result.</summary>
    /// <param name="result">The complete immutable terminal record.</param>
    /// <returns>An algorithm-qualified digest over all request evidence.</returns>
    public static InputFingerprint Fingerprint(DurableOperationResult result) =>
        SecurityCanonicalFingerprint.Create(result);

    /// <summary>Fingerprints one exact waiting record.</summary>
    /// <param name="waiting">The complete immutable waiting record.</param>
    /// <returns>An algorithm-qualified digest over all request evidence.</returns>
    public static InputFingerprint Fingerprint(DurableOperationWaiting waiting) =>
        SecurityCanonicalFingerprint.Create(waiting);

    /// <summary>Fingerprints one exact evidence load address.</summary>
    /// <param name="address">The operation address.</param>
    /// <returns>An algorithm-qualified digest over all request evidence.</returns>
    public static InputFingerprint Fingerprint(DurableOperationAddress address) =>
        SecurityCanonicalFingerprint.Create(address);
}
