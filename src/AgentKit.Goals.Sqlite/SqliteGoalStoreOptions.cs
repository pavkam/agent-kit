// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

/// <summary>Configures the lock and size bounds of a SQLite goal store.</summary>
public sealed class SqliteGoalStoreOptions
{
    /// <summary>Gets or sets the whole-second wait for another writer.</summary>
    /// <value>At least one second. The default is five seconds.</value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the largest encoded goal document, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets the scanner identities allowed to call <see cref="IGoalStore.ReadIntentsAsync"/>.</summary>
    /// <value>A list that is empty by default: intent discovery crosses tenants and is refused unless the host names its worker.</value>
    public List<ComponentId> AuthorizedIntentScanners { get; } = [];
}
