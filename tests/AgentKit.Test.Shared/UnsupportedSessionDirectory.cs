// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="ISessionDirectory"/> test double that throws <see cref="NotSupportedException"/> from every operation.</summary>
/// <remarks>A placeholder collaborator for tests whose scripted coordinator never locates or records a session route.</remarks>
public sealed class UnsupportedSessionDirectory: ISessionDirectory
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.test.unsupported-directory");

    /// <inheritdoc/>
    public bool Durable => false;

    /// <inheritdoc/>
    public ValueTask<SessionLocationResult> LocateAsync(
        AuthorizedSessionDirectoryRequest<SessionOperationContext> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support location.");

    /// <inheritdoc/>
    public ValueTask<SessionCreationLocationResult> LocateForCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionCreateRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support creation location.");

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryWriteResult> RecordAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support recording.");

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryWriteResult> RecordCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support create recording.");
}
