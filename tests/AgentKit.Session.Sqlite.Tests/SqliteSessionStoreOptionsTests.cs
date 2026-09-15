// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

/// <summary>Verifies the mutable composition options agree with the immutable settings defaults.</summary>
public sealed class SqliteSessionStoreOptionsTests
{
    [Fact]
    public void Constructor_WhenCreated_DefaultsEqualCreateDefaultSettings()
    {
        var options = new SqliteSessionStoreOptions();
        var defaults = SqliteSessionStoreSettings.CreateDefault();

        options.LockTimeout.ShouldBe(defaults.LockTimeout);
        options.MaximumEntryPayloadBytes.ShouldBe(defaults.MaximumEntryPayloadBytes);
        options.MaximumIssuedReadSnapshots.ShouldBe(defaults.MaximumIssuedReadSnapshots);
    }

    [Fact]
    public void Constructor_WhenDefaultsAreMaterialized_ProducesSettingsEqualToCreateDefault()
    {
        var options = new SqliteSessionStoreOptions();

        var settings = new SqliteSessionStoreSettings(
            options.LockTimeout, options.MaximumEntryPayloadBytes, options.MaximumIssuedReadSnapshots);

        settings.ShouldBe(SqliteSessionStoreSettings.CreateDefault());
    }

    [Fact]
    public void Setters_WhenAssigned_DoNotValidate()
    {
        // Validation is deferred to SqliteSessionStoreSettings so a delegate may assign in any order.
        var options = new SqliteSessionStoreOptions
        {
            LockTimeout = TimeSpan.Zero,
            MaximumEntryPayloadBytes = -1,
            MaximumIssuedReadSnapshots = 0,
        };

        options.LockTimeout.ShouldBe(TimeSpan.Zero);
        options.MaximumEntryPayloadBytes.ShouldBe(-1);
        options.MaximumIssuedReadSnapshots.ShouldBe(0);
    }
}
