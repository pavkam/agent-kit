// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authoritatively locates and conditionally records tenant-partitioned session store routes.</summary>
/// <remarks>Every call is a protected effecting boundary that validates and consumes its own directory grant. Implementations mask cross-tenant existence and never fall back to probing stores.</remarks>
public interface ISessionDirectory
{
    /// <summary>Gets the sole security audience permitted to consume grants for this directory.</summary>
    /// <value>A stable nonblank component identity used by exact enforcement.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Gets whether locations survive process loss.</summary>
    /// <value><see langword="true"/> only when authoritative routing records are durable.</value>
    public bool Durable { get; }

    /// <summary>Locates the visible authoritative store route for one session.</summary>
    /// <param name="request">The authorized operation context and directory-specific grant.</param>
    /// <param name="cancellationToken">Cancels before directory access or grant consumption commits.</param>
    /// <returns>The visible location, a tenant-safe missing outcome, or an unavailable outcome.</returns>
    public ValueTask<SessionLocationResult> LocateAsync(AuthorizedSessionDirectoryRequest<SessionOperationContext> request,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up a prior creation route by the canonical outer retry identity before allocating a session identity.</summary>
    /// <param name="request">The authorized sessionless outer creation request and creation-lookup grant.</param>
    /// <param name="cancellationToken">Cancels before directory access or grant consumption commits.</param>
    /// <returns>The prior visible route, a tenant-safe missing or denied outcome, or an unavailable outcome.</returns>
    public ValueTask<SessionCreationLocationResult> LocateForCreateAsync(AuthorizedSessionDirectoryRequest<SessionCreateRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Conditionally records a selected store route before the store is created.</summary>
    /// <param name="request">The authorized conditional write and directory-specific grant.</param>
    /// <param name="cancellationToken">Cancels before directory access or grant consumption commits.</param>
    /// <returns>The committed, idempotent, conflicting, or unavailable write outcome.</returns>
    public ValueTask<SessionDirectoryWriteResult> RecordAsync(AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>Conditionally records a newly allocated creation route before the selected store is invoked.</summary>
    /// <param name="request">The authorized canonical creation request and candidate route.</param>
    /// <param name="cancellationToken">Cancels before directory access or grant consumption commits.</param>
    /// <returns>The committed, reconciled, conflicting, denied, or unavailable result without exposing another tenant's state.</returns>
    public ValueTask<SessionDirectoryWriteResult> RecordCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request,
        CancellationToken cancellationToken = default);
}
