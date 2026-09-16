// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies SqliteBudgetLedgerSettings behavior and contracts.</summary>
public sealed class SqliteBudgetLedgerSettingsTests
{
    /// <summary>Verifies valid bounds are captured exactly.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var lockTimeout = TimeSpan.FromSeconds(3);
        var settings = new SqliteBudgetLedgerSettings(lockTimeout, 1024, 2048, 16, 8, 4);
        settings.LockTimeout.ShouldBe(lockTimeout);
        settings.MaximumPayloadBytes.ShouldBe(1024);
        settings.MaximumResultBytes.ShouldBe(2048);
        settings.MaximumBatchSize.ShouldBe(16);
        settings.MaximumLimitsPerScope.ShouldBe(8);
        settings.MaximumLineageDepth.ShouldBe(4);
    }

    /// <summary>Verifies a lock timeout with a sub-second remainder is rejected.</summary>
    [Fact]
    public void Constructor_WhenLockTimeoutHasSubSecondRemainder_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteBudgetLedgerSettings(
            TimeSpan.FromMilliseconds(1500), 1024, 1024, 16, 8, 4)).ParamName.ShouldBe("lockTimeout");
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured bound.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var original = SqliteBudgetLedgerSettings.CreateDefault();
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }
}
