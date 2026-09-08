// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Security.Cryptography;
using System.Text;

/// <summary>Produces canonical security resources and fingerprints for session-directory access.</summary>
/// <remarks>Directory grants bind the complete immutable request evidence through the shared canonical encoder. Resources remain content-free, and the creation resource uses a digest of the caller retry key rather than placing caller input in a diagnostic identifier.</remarks>
public static class SessionDirectorySecurityBinding
{
    private const string _schema = "agentkit.session-directory-security-binding/v1";

    /// <summary>Names one tenant-partitioned session-directory location.</summary>
    /// <param name="tenantId">The nonblank tenant partition.</param>
    /// <param name="address">The non-null complete session address.</param>
    /// <returns>The canonical protected application-state resource.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public static ProtectedResource Resource(TenantId tenantId, SessionAddress address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(address);
        return new ProtectedResource(
            ProtectedResourceKind.ApplicationState,
            $"session-directory:{tenantId}/{address.AgentId}/{address.SessionId}");
    }

    /// <summary>Names the tenant-partitioned idempotency route used before a session identity is allocated.</summary>
    /// <param name="tenantId">The nonblank tenant partition.</param>
    /// <param name="agentId">The non-default owning agent identity.</param>
    /// <param name="idempotencyKey">The nonblank outer creation retry key.</param>
    /// <returns>A content-free canonical resource for one retry-identity route.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> or <paramref name="idempotencyKey"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default.</exception>
    public static ProtectedResource CreationResource(TenantId tenantId, AgentId agentId, IdempotencyKey idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return new ProtectedResource(
            ProtectedResourceKind.ApplicationState,
            $"session-directory-create:{tenantId}/{agentId}/sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey.Value)))}");
    }

    /// <summary>Fingerprints the exact directory lookup represented by an operation context.</summary>
    /// <param name="context">The non-null context requesting the lookup.</param>
    /// <returns>An algorithm-qualified digest over the complete lookup evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static InputFingerprint LocateFingerprint(SessionOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SecurityCanonicalFingerprint.Create(new DirectoryLocatePayload(_schema, context));
    }

    /// <summary>Fingerprints the exact conditional directory record request.</summary>
    /// <param name="request">The non-null directory write request.</param>
    /// <returns>An algorithm-qualified digest over the complete route and retry evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint RecordFingerprint(SessionDirectoryWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SecurityCanonicalFingerprint.Create(new DirectoryRecordPayload(_schema, request));
    }

    /// <summary>Fingerprints the exact sessionless creation lookup request.</summary>
    /// <param name="request">The non-null canonical outer creation request.</param>
    /// <returns>An algorithm-qualified digest over complete creation and authorization evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint LocateForCreateFingerprint(SessionCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SecurityCanonicalFingerprint.Create(new DirectoryCreationLookupPayload(_schema, request));
    }

    /// <summary>Fingerprints the exact conditional creation-route record request.</summary>
    /// <param name="request">The non-null outer creation request and candidate route.</param>
    /// <returns>An algorithm-qualified digest over the complete creation and routing evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint RecordCreateFingerprint(SessionDirectoryCreateRecordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SecurityCanonicalFingerprint.Create(new DirectoryCreationRecordPayload(_schema, request));
    }

    /// <summary>Provides canonical immutable payload shape for one existing-session lookup.</summary>
    private sealed record DirectoryLocatePayload(string Schema, SessionOperationContext Context);

    /// <summary>Provides canonical immutable payload shape for one existing-session route write.</summary>
    private sealed record DirectoryRecordPayload(string Schema, SessionDirectoryWriteRequest Request);

    /// <summary>Provides canonical immutable payload shape for one pre-allocation creation lookup.</summary>
    private sealed record DirectoryCreationLookupPayload(string Schema, SessionCreateRequest Request);

    /// <summary>Provides canonical immutable payload shape for one pre-store creation-route write.</summary>
    private sealed record DirectoryCreationRecordPayload(string Schema, SessionDirectoryCreateRecordRequest Request);
}
