// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies the immutable new-session request and compiled profile used to select its initial store.</summary>
/// <remarks>Selection uses only the profile's explicit default key. It does not create directory state or authorize a store effect.</remarks>
public sealed record SessionStoreCreateSelectionRequest
{
    /// <summary>Initializes a create-store selection request.</summary>
    /// <param name="request">The non-null new-session request.</param>
    /// <param name="profile">The non-null compiled session profile.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="request"/> has a default agent identity.</exception>
    /// <exception cref="ArgumentException"><paramref name="request"/> has a blank idempotency key.</exception>
    public SessionStoreCreateSelectionRequest(SessionCreateRequest request, SessionProfileSnapshot profile)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(request.AgentId, default, nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey.Value, nameof(request));
        Request = request;
        Profile = profile;
    }

    /// <summary>Gets the new-session request.</summary><value>The non-null request validated before selection.</value>
    public SessionCreateRequest Request { get; }
    /// <summary>Gets the compiled profile.</summary><value>The non-null immutable profile that names the initial store.</value>
    public SessionProfileSnapshot Profile { get; }
}
