// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

using AgentKit.Conformance;

internal static class JsonSecurityStoreTestBootstrap
{
    extension(JsonSecurityGrantStore store)
    {
        public ValueTask InitializeTrustedAsync(
            TimeProvider? timeProvider = null,
            CancellationToken cancellationToken = default) =>
            store.InitializeAsync(
                SecurityControlPlaneTestBootstrap.Create(timeProvider ?? TimeProvider.System),
                cancellationToken);
    }

    extension(JsonApprovalStore store)
    {
        public ValueTask InitializeTrustedAsync(
            TimeProvider? timeProvider = null,
            CancellationToken cancellationToken = default) =>
            store.InitializeAsync(
                SecurityControlPlaneTestBootstrap.Create(timeProvider ?? TimeProvider.System),
                cancellationToken);
    }

    extension(JsonSecurityDecisionStore store)
    {
        public ValueTask InitializeTrustedAsync(
            TimeProvider? timeProvider = null,
            CancellationToken cancellationToken = default) =>
            store.InitializeAsync(
                SecurityControlPlaneTestBootstrap.Create(timeProvider ?? TimeProvider.System),
                cancellationToken);
    }
}
