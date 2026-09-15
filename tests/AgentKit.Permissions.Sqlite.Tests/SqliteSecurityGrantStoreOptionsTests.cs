// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Verifies the mutable composition-time options mirror the immutable settings defaults.</summary>
public sealed class SqliteSecurityGrantStoreOptionsTests
{
    /// <summary>Verifies a fresh options instance materializes to settings equal to the documented defaults.</summary>
    [Fact]
    public void Constructor_WhenUnmodified_MatchesSettingsDefaults()
    {
        var expected = SqliteSecurityGrantStoreSettings.CreateDefault();

        var options = new SqliteSecurityGrantStoreOptions();
        var actual = new SqliteSecurityGrantStoreSettings(
            options.LockTimeout,
            options.MaximumGrantBytes,
            options.MaximumEnforcementBytes,
            options.MaximumResources,
            options.MaximumClaims,
            options.MaximumDelegationLinks);

        actual.ShouldBe(expected);
        options.LockTimeout.ShouldBe(expected.LockTimeout);
        options.MaximumGrantBytes.ShouldBe(expected.MaximumGrantBytes);
        options.MaximumEnforcementBytes.ShouldBe(expected.MaximumEnforcementBytes);
        options.MaximumResources.ShouldBe(expected.MaximumResources);
        options.MaximumClaims.ShouldBe(expected.MaximumClaims);
        options.MaximumDelegationLinks.ShouldBe(expected.MaximumDelegationLinks);
    }

    /// <summary>Verifies every property is independently settable so a configure delegate can adjust one bound at a time.</summary>
    [Fact]
    public void Properties_WhenSet_RetainAssignedValues()
    {
        var options = new SqliteSecurityGrantStoreOptions
        {
            LockTimeout = TimeSpan.FromSeconds(2),
            MaximumGrantBytes = 1,
            MaximumEnforcementBytes = 2,
            MaximumResources = 3,
            MaximumClaims = 4,
            MaximumDelegationLinks = 5,
        };

        options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        options.MaximumGrantBytes.ShouldBe(1);
        options.MaximumEnforcementBytes.ShouldBe(2);
        options.MaximumResources.ShouldBe(3);
        options.MaximumClaims.ShouldBe(4);
        options.MaximumDelegationLinks.ShouldBe(5);
    }
}
