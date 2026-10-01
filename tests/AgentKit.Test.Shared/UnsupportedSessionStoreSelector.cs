// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="ISessionStoreSelector"/> test double that throws <see cref="NotSupportedException"/> from every member.</summary>
/// <remarks>A placeholder collaborator for tests whose scripted coordinator never selects a store.</remarks>
public sealed class UnsupportedSessionStoreSelector: ISessionStoreSelector
{
    /// <inheritdoc/>
    public ValueTask<SessionStoreSelectionResult> SelectForCreateAsync(
        SessionStoreCreateSelectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support store selection.");

    /// <inheritdoc/>
    public ValueTask<SessionStoreSelectionResult> ResolveExistingAsync(
        SessionStoreSelectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support store resolution.");
}
