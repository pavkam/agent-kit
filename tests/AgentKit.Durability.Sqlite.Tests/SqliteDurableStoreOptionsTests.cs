// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies composition-time options start from the documented defaults and stay mutable until frozen.</summary>
public sealed class SqliteDurableStoreOptionsTests
{
    /// <summary>Verifies the defaults match the immutable settings a caller gets without configuring anything.</summary>
    [Fact]
    public void Constructor_WhenCreated_MatchesTheDocumentedSettingsDefaults()
    {
        var options = new SqliteDurableStoreOptions();

        options.LockTimeout.ShouldBe(SqliteDurableStoreSettings.CreateDefault().LockTimeout);
        options.MaximumRecordBytes.ShouldBe(SqliteDurableStoreSettings.CreateDefault().MaximumRecordBytes);
    }

    /// <summary>Verifies mutated values survive into the immutable settings a registration materializes.</summary>
    [Fact]
    public void Properties_WhenMutated_MaterializeIntoTheImmutableSettings()
    {
        var options = new SqliteDurableStoreOptions
        {
            LockTimeout = TimeSpan.FromSeconds(9),
            MaximumRecordBytes = 4096,
        };

        var settings = new SqliteDurableStoreSettings(options.LockTimeout, options.MaximumRecordBytes);

        settings.LockTimeout.ShouldBe(TimeSpan.FromSeconds(9));
        settings.MaximumRecordBytes.ShouldBe(4096);
    }
}
