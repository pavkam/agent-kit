// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Produces the protected resource and canonical fingerprint that bind a delegation grant to one exact delegation.</summary>
/// <remarks>The coordinator asks the authority for a <see cref="SecurityOperationKind.Delegation"/> grant bound to these values and the dispatcher consumes it immediately before acting, so a grant for one delegation cannot dispatch another.</remarks>
public static class DelegationSecurityBinding
{
    /// <summary>Names one delegation as a protected resource.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <returns>The protected delegation resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    public static ProtectedResource Resource(DelegationId id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        return new(ProtectedResourceKind.Delegation, $"delegation:{id}");
    }

    /// <summary>Fingerprints the exact canonical delegation, including the captured authorization and narrowed scope and budget.</summary>
    /// <param name="request">The canonical request.</param>
    /// <returns>A deterministic fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint Fingerprint(DelegationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SecurityCanonicalFingerprint.Create(request);
    }
}
