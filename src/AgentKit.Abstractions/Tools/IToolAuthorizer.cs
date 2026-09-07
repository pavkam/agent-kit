// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Decides whether one resolved tool call is authorized to proceed.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately reduced stand-in for the general
/// <c>ISecurityAuthority</c> described by the tools-and-permissions
/// architecture, scoped to tool calls alone and without approval, budget,
/// or audit integration. Every protected tool call still passes through
/// this seam before invocation, so replacing it with a full security
/// authority later is a registration change, not a call-site change.
/// </para>
/// <para>
/// Implementations must be safe to call concurrently for independent
/// requests, must fail closed (deny) when authorization cannot be
/// determined, and must never grant based on tool or MCP-supplied
/// metadata such as a tool's own description or declared effect —
/// authorization decisions are based on the caller's identity and the
/// tool's resolved, catalog-registered identity only.
/// </para>
/// </remarks>
public interface IToolAuthorizer
{
    /// <summary>Authorizes one resolved tool call.</summary>
    /// <param name="request">The authorization request.</param>
    /// <param name="cancellationToken">A token used to cancel the authorization check.</param>
    /// <returns>A task producing the closed authorization outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<ToolAuthorizationDecision> AuthorizeAsync(
        ToolAuthorizationRequest request, CancellationToken cancellationToken = default);
}
