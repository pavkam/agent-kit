// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Initializes SQLite security stores with trusted bootstrap evidence in tests.</summary>
internal static class SqliteSecurityStoreTestBootstrap
{
    extension(SqliteSecurityGrantStore store)
    {
        /// <summary>Initializes the grant store schema and records test bootstrap evidence.</summary>
        /// <param name="timeProvider">The clock used to stamp bootstrap evidence.</param>
        /// <param name="cancellationToken">Cancels before initialization completes.</param>
        public ValueTask InitializeTrustedAsync(
            TimeProvider? timeProvider = null,
            CancellationToken cancellationToken = default) =>
            store.InitializeAsync(
                SecurityControlPlaneTestBootstrap.Create(timeProvider ?? TimeProvider.System),
                cancellationToken);
    }

    extension(SqliteApprovalStore store)
    {
        /// <summary>Initializes the approval store schema and records test bootstrap evidence.</summary>
        /// <param name="timeProvider">The clock used to stamp bootstrap evidence.</param>
        /// <param name="cancellationToken">Cancels before initialization completes.</param>
        public ValueTask InitializeTrustedAsync(
            TimeProvider? timeProvider = null,
            CancellationToken cancellationToken = default) =>
            store.InitializeAsync(
                SecurityControlPlaneTestBootstrap.Create(timeProvider ?? TimeProvider.System),
                cancellationToken);
    }

    extension(SqliteSecurityDecisionStore store)
    {
        /// <summary>Initializes the decision store schema and records test bootstrap evidence.</summary>
        /// <param name="timeProvider">The clock used to stamp bootstrap evidence.</param>
        /// <param name="cancellationToken">Cancels before initialization completes.</param>
        public ValueTask InitializeTrustedAsync(
            TimeProvider? timeProvider = null,
            CancellationToken cancellationToken = default) =>
            store.InitializeAsync(
                SecurityControlPlaneTestBootstrap.Create(timeProvider ?? TimeProvider.System),
                cancellationToken);
    }
}
