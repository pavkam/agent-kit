// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Targets one already allocated session address for the selected store's atomic creation effect.</summary>
/// <remarks>Session identity allocation and directory routing precede this lower store request. The store persists the exact address instead of fabricating another identity, so directory and store truth cannot diverge.</remarks>
public sealed record SessionStoreCreateRequest
{
    /// <summary>Initializes a lower session-store creation request.</summary>
    /// <param name="request">The complete logical creation request captured before routing.</param>
    /// <param name="address">The exact newly allocated address to persist.</param>
    /// <param name="context">Fresh session-bound authorization and identity evidence for the allocated address.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/>, <paramref name="address"/>, or <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The address, context, identity, causal operation, or captured selection differs from the logical creation request, or the context is lane-bound.</exception>
    public SessionStoreCreateRequest(SessionCreateRequest request, SessionAddress address, SessionOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNotEqual(address.AgentId, request.AgentId, nameof(address));
        ArgumentException.ThrowIfNotEqual(context.ToAddress(), address, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Identity, request.Identity, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Correlation, request.Authorization.Scope.Correlation, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Authorization.ProfileKey, request.Authorization.ProfileKey, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Authorization.ProfileVersion, request.Authorization.ProfileVersion, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Authorization.PolicySnapshot, request.Authorization.PolicySnapshot, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Authorization.AuthorityKey, request.Authorization.AuthorityKey, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Authorization.AgentDefinitionRevision, request.Authorization.AgentDefinitionRevision, nameof(context));
        ArgumentException.ThrowIfNotEqual(context.Authorization.ConfigurationVersion, request.Authorization.ConfigurationVersion, nameof(context));
        if (context.ExecutionLaneId is not null)
        {
            throw new ArgumentException("Session creation context must describe session-wide work without a lane.", nameof(context));
        }
        Request = request;
        Address = address;
        Context = context;
    }

    /// <summary>Gets the logical creation request.</summary>
    /// <value>The complete identity, authorization, idempotency, conversation, and extension evidence selected for creation.</value>
    public SessionCreateRequest Request { get; }

    /// <summary>Gets the address allocated before store selection.</summary>
    /// <value>The exact non-default agent and session identity the store must create.</value>
    public SessionAddress Address { get; }

    /// <summary>Gets the fresh session-bound operation context.</summary>
    /// <value>Captured authorization and identity evidence that exactly binds <see cref="Address"/> while preserving the logical creation selection and cause.</value>
    public SessionOperationContext Context { get; }
}
