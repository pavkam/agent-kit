// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

public sealed class SqliteEvaluationStoreSettingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(1500)]
    public void Constructor_WhenTimeoutIsShorterThanASecondOrNotWholeSeconds_ThrowsArgumentOutOfRangeException(int milliseconds) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreSettings(TimeSpan.FromMilliseconds(milliseconds), 1)).ParamName.ShouldBe("lockTimeout");

    [Fact]
    public void Constructor_WhenTimeoutIsTooLarge_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreSettings(TimeSpan.FromDays(100_000), 1)).ParamName.ShouldBe("lockTimeout");

    [Fact]
    public void Constructor_WhenSizeBoundIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreSettings(TimeSpan.FromSeconds(1), 0)).ParamName.ShouldBe("maximumRecordBytes");

    [Fact]
    public void CreateDefault_WhenCalled_UsesFiveSecondsAndOneMebibyte()
    {
        var settings = SqliteEvaluationStoreSettings.CreateDefault();

        (settings.LockTimeout, settings.MaximumRecordBytes).ShouldBe((TimeSpan.FromSeconds(5), 1_048_576));
    }
}
