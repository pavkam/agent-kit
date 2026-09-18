// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>A minimal foreign <see cref="ISessionDirectory"/> used only to prove <c>TryAdd</c> directory composition.</summary>
/// <remarks>No method is ever invoked; the type exists solely to be resolved from dependency injection in place of the JSON directory.</remarks>
internal sealed class FakeSessionDirectory: ISessionDirectory
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("test.fake.session.directory");

    /// <inheritdoc/>
    public bool Durable => false;

    /// <inheritdoc/>
    public ValueTask<SessionLocationResult> LocateAsync(
        AuthorizedSessionDirectoryRequest<SessionOperationContext> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake directory is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionCreationLocationResult> LocateForCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionCreateRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake directory is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryWriteResult> RecordAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake directory is never invoked.");

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryWriteResult> RecordCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake directory is never invoked.");
}
