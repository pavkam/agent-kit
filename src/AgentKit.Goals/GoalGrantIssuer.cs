// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Obtains single-use grants from the security authority a captured authorization names.</summary>
/// <remarks>
/// The issuer selects the authority recorded on the captured authorization, never the agent's latest definition, so delayed
/// work runs under the authority and security profile it began under. A selection that does not return exactly the captured
/// authorization, a denial, or an unavailable authority all yield no grant; the caller then fails closed before any effect.
/// </remarks>
internal sealed class GoalGrantIssuer
{
    private readonly ISecurityAuthoritySelector _authorities;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _time;
    private readonly AgentGoalOptions _options;

    /// <summary>Initializes the issuer.</summary>
    /// <param name="authorities">The selector that resolves the authority a captured authorization names.</param>
    /// <param name="requestIds">The allocator of security-request identities.</param>
    /// <param name="time">The clock used for the grant's deadline.</param>
    /// <param name="options">The host options carrying the grant lifetime.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public GoalGrantIssuer(
        ISecurityAuthoritySelector authorities,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider time,
        IOptions<AgentGoalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(authorities);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        _authorities = authorities;
        _requestIds = requestIds;
        _time = time;
        _options = options.Value;
    }

    /// <summary>Asks the captured authority for one grant bound to one exact operation.</summary>
    /// <param name="authorization">The captured authorization the operation runs under.</param>
    /// <param name="audience">The component that will consume the grant.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The protected effect.</param>
    /// <param name="resources">The protected resources.</param>
    /// <param name="fingerprint">The canonical fingerprint of the exact operation.</param>
    /// <param name="notAfter">An earlier deadline than the configured lifetime, or <see langword="null"/>.</param>
    /// <param name="hooks">The live hook context, or <see langword="null"/> for delayed work.</param>
    /// <param name="cancellationToken">Cancels authorization.</param>
    /// <returns>The grant, or a content-safe reason and whether the authority was unavailable rather than denying.</returns>
    internal async ValueTask<GrantIssue> IssueAsync(
        SecurityAuthorizationContext authorization,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        DateTimeOffset? notAfter,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken)
    {
        Debug.Assert(authorization is not null, "Callers supply captured authorization.");
        SecurityAuthoritySelectionResult selection;
        try
        {
            selection = await _authorities.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GrantIssue.Unavailable("The captured security authority could not be selected.");
        }

        if (selection is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return GrantIssue.Unavailable("The captured security authority is unavailable.");
        }

        var deadline = _time.GetUtcNow().Add(_options.GrantLifetime);
        if (notAfter is { } limit && limit < deadline)
        {
            deadline = limit;
        }

        var request = new SecurityRequest(
            _requestIds.Create(), authorization.Scope, toolCallId: null, authorization.Identity, authorization, audience, kind, effect,
            resources, fingerprint, deadline);
        SecurityDecision decision;
        try
        {
            decision = await selected.Authority.AuthorizeAsync(request, hooks, cancellationToken).ConfigureAwait(false);
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

        internal static GrantIssue Granted(SecurityGrant grant) => new(grant, null, false);

        internal static GrantIssue Denied(string message) => new(null, message, false);

        internal static GrantIssue Unavailable(string message) => new(null, message, true);
    }
}
