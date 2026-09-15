// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies the mutable composition-time options mirror the immutable settings defaults.</summary>
public sealed class SqliteBudgetLedgerOptionsTests
{
    /// <summary>Proves fresh options materialize to exactly <see cref="SqliteBudgetLedgerSettings.CreateDefault"/>.</summary>
    [Fact]
    public void Constructor_WhenUnconfigured_MatchesSettingsDefaults()
    {
        var options = new SqliteBudgetLedgerOptions();

        var settings = new SqliteBudgetLedgerSettings(
            options.LockTimeout,
            options.MaximumPayloadBytes,
            options.MaximumResultBytes,
            options.MaximumBatchSize,
            options.MaximumLimitsPerScope,
            options.MaximumLineageDepth);

        settings.ShouldBe(SqliteBudgetLedgerSettings.CreateDefault());
    }

    /// <summary>Proves each default value individually equals its documented bound.</summary>
    [Fact]
    public void Constructor_WhenUnconfigured_ExposesDocumentedDefaults()
    {
        var options = new SqliteBudgetLedgerOptions();

        options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.MaximumPayloadBytes.ShouldBe(1_048_576);
        options.MaximumResultBytes.ShouldBe(1_048_576);
        options.MaximumBatchSize.ShouldBe(256);
        options.MaximumLimitsPerScope.ShouldBe(256);
        options.MaximumLineageDepth.ShouldBe(32);
    }

    /// <summary>Proves the options are plain mutable state and do not validate on assignment; validation belongs to materialization.</summary>
    [Fact]
    public void Properties_WhenAssignedInvalidValues_DoNotThrowUntilMaterialized()
    {
        var options = new SqliteBudgetLedgerOptions { MaximumBatchSize = 0, LockTimeout = TimeSpan.Zero };

        options.MaximumBatchSize.ShouldBe(0);
        options.LockTimeout.ShouldBe(TimeSpan.Zero);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteBudgetLedgerSettings(
            options.LockTimeout,
            options.MaximumPayloadBytes,
            options.MaximumResultBytes,
            options.MaximumBatchSize,
            options.MaximumLimitsPerScope,
            options.MaximumLineageDepth));
    }
}
