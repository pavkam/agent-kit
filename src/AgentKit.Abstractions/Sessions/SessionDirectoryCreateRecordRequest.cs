// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests the conditional recording of a newly allocated route before store creation.</summary>
/// <remarks>The outer request remains the canonical caller retry identity and has a truthful sessionless authorization scope. The allocated location supplies the newly session-bound address without fabricating a prior session context.</remarks>
public sealed record SessionDirectoryCreateRecordRequest
{
    /// <summary>Initializes a creation-route recording request.</summary>
    /// <param name="request">The non-null canonical outer creation request.</param>
    /// <param name="location">The non-null candidate location to record before store creation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="location"/> is null.</exception>
    /// <exception cref="ArgumentException">The location does not match the request's agent or tenant, or its idempotency key is blank.</exception>
    public SessionDirectoryCreateRecordRequest(SessionCreateRequest request, SessionLocation location)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey.Value, nameof(request));
        ArgumentException.ThrowIfInvalidSessionDirectoryCreationBinding(request, location, nameof(location));

        Request = request; Location = location;
    }

    /// <summary>Gets the canonical caller creation request.</summary><value>The non-null request whose idempotency identity must be reconciled before allocation.</value>
    public SessionCreateRequest Request { get; }
    /// <summary>Gets the candidate pre-store location.</summary><value>The non-null route that is conditionally recorded before store creation.</value>
    public SessionLocation Location { get; }
}
