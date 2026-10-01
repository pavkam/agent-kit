// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Obtains single-use grants from the security authority a captured authorization names.</summary>
/// <remarks>
/// The issuer selects the authority recorded on the captured authorization through the selector a profile lease captured, never
/// the agent's latest definition, so delayed work runs under the authority and security profile it began under. A selection that
/// does not return exactly the captured authorization, a denial, or an unavailable authority all yield no grant; the caller then
/// fails closed before any effect. The issuer never reuses a grant for a different concrete effect.
/// </remarks>
/// <param name="requestIds">The allocator of security-request identities.</param>
/// <param name="time">The clock used for each grant's deadline.</param>
/// <param name="options">The engine-wide options carrying the grant lifetime.</param>
internal sealed class MemoryGrantIssuer(IIdentifierGenerator<SecurityRequestId> requestIds, TimeProvider time, IOptions<AgentMemoryOptions> options)
{
    /// <summary>Asks the captured authority for one grant bound to one exact operation.</summary>
    /// <param name="authorities">The selector that resolves the authority a captured authorization names.</param>
    /// <param name="authorization">The captured authorization the operation runs under.</param>
    /// <param name="audience">The component that will consume the grant.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The protected effect.</param>
    /// <param name="resources">The protected resources.</param>
    /// <param name="fingerprint">The canonical fingerprint of the exact operation.</param>
    /// <param name="cancellationToken">Cancels authorization.</param>
    /// <returns>The grant, or a content-safe reason and whether the authority was unavailable rather than denying.</returns>
    internal async ValueTask<GrantIssue> IssueAsync(
        ISecurityAuthoritySelector authorities,
        SecurityAuthorizationContext authorization,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        Debug.Assert(authorities is not null, "Callers supply the lease's authority selector.");
        Debug.Assert(authorization is not null, "Callers supply captured authorization.");
        SecurityAuthoritySelectionResult selection;
        try
        {
            selection = await authorities.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GrantIssue.Unavailable("The captured security authority could not be selected.");
        }

        if (selection is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return GrantIssue.Unavailable("The captured security authority is unavailable.");
        }

        var request = new SecurityRequest(
            requestIds.Create(), authorization.Scope, toolCallId: null, authorization.Identity, authorization, audience, kind, effect,
            resources, fingerprint, time.GetUtcNow().Add(options.Value.GrantLifetime));
        SecurityDecision decision;
        try
        {
            decision = await selected.Authority.AuthorizeAsync(request, hooks: null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GrantIssue.Unavailable("The security authority failed while authorizing.");
        }

        return decision switch
        {
            SecurityAllowed allowed => GrantIssue.Granted(allowed.Grant),
            SecurityDenied denied => GrantIssue.Denied(denied.Denial.SafeMessage),
            _ => GrantIssue.Denied("The security authority did not allow the operation."),
        };
    }

    /// <summary>Is the outcome of asking for a grant.</summary>
    internal sealed class GrantIssue
    {
        private GrantIssue(SecurityGrant? grant, string? message, bool unavailable)
        {
            Grant = grant;
            SafeMessage = message;
            IsUnavailable = unavailable;
        }

        /// <summary>Gets the grant, or <see langword="null"/> when none was issued.</summary>
        internal SecurityGrant? Grant { get; }

        /// <summary>Gets the content-safe reason no grant was issued, or <see langword="null"/>.</summary>
        internal string? SafeMessage { get; }

        /// <summary>Gets a value indicating whether the authority was unavailable rather than denying.</summary>
        internal bool IsUnavailable { get; }

        /// <summary>Creates a granted outcome.</summary>
        /// <param name="grant">The issued grant.</param>
        /// <returns>The outcome.</returns>
        internal static GrantIssue Granted(SecurityGrant grant) => new(grant, null, false);

        /// <summary>Creates a denied outcome.</summary>
        /// <param name="message">The content-safe reason.</param>
        /// <returns>The outcome.</returns>
        internal static GrantIssue Denied(string message) => new(null, message, false);

        /// <summary>Creates an unavailable outcome.</summary>
        /// <param name="message">The content-safe reason.</param>
        /// <returns>The outcome.</returns>
        internal static GrantIssue Unavailable(string message) => new(null, message, true);

        /// <summary>Converts a failed outcome to the store failure a caller returns.</summary>
        /// <returns>A denied or unavailable failure carrying the reason.</returns>
        internal MemoryStoreFailure ToFailure() => new(
            IsUnavailable ? MemoryStoreFailureKind.Unavailable : MemoryStoreFailureKind.Denied,
            SafeMessage ?? "The operation was not authorized.");
    }
}
