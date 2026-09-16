// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

/// <summary>Verifies the validated immutable SQLite session bounds.</summary>
public sealed class SqliteSessionStoreSettingsTests
{
    [Fact]
    public void CreateDefault_WhenCalled_ReturnsDocumentedDefaults()
    {
        var settings = SqliteSessionStoreSettings.CreateDefault();

        settings.LockTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        settings.MaximumEntryPayloadBytes.ShouldBe(1_048_576);
        settings.MaximumIssuedReadSnapshots.ShouldBe(4096);
    }

    [Fact]
    public void Constructor_WhenSnapshotBoundOmitted_DefaultsToFourThousandNinetySix()
    {
        var settings = new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), 1);

        settings.MaximumIssuedReadSnapshots.ShouldBe(4096);
    }

    [Fact]
    public void Constructor_WhenAllBoundsValid_PreservesValues()
    {
        var settings = new SqliteSessionStoreSettings(TimeSpan.FromSeconds(9), 77, 1);

        settings.LockTimeout.ShouldBe(TimeSpan.FromSeconds(9));
        settings.MaximumEntryPayloadBytes.ShouldBe(77);
        settings.MaximumIssuedReadSnapshots.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WhenMaximumIssuedReadSnapshotsIsNotPositive_ThrowsExactParameter(int value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), 1, value));

        exception.ParamName.ShouldBe("maximumIssuedReadSnapshots");
        exception.ActualValue.ShouldBe(value);
    }

    [Fact]
    public void Constructor_WhenMaximumIssuedReadSnapshotsIsMaxValue_Accepts() =>
        new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), 1, int.MaxValue).MaximumIssuedReadSnapshots.ShouldBe(int.MaxValue);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumEntryPayloadBytesIsNotPositive_ThrowsExactParameter(int value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), value));

        exception.ParamName.ShouldBe("maximumEntryPayloadBytes");
    }

    [Fact]
    public void Constructor_WhenLockTimeoutIsBelowOneSecond_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSessionStoreSettings(TimeSpan.FromMilliseconds(999), 1))
            .ParamName.ShouldBe("lockTimeout");

    [Fact]
    public void Constructor_WhenLockTimeoutIsFractional_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSessionStoreSettings(TimeSpan.FromMilliseconds(1_500), 1))
            .ParamName.ShouldBe("lockTimeout");

    [Fact]
    public void Constructor_WhenLockTimeoutExceedsIntSeconds_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSessionStoreSettings(TimeSpan.FromSeconds((double) int.MaxValue + 1), 1))
            .ParamName.ShouldBe("lockTimeout");

    [Fact]
    public void ToString_WhenCalled_IncludesCapturedFieldNames()
    {
        var settings = SqliteSessionStoreSettings.CreateDefault();

        var text = settings.ToString();

        text.ShouldContain(nameof(SqliteSessionStoreSettings.LockTimeout));
        text.ShouldContain(nameof(SqliteSessionStoreSettings.MaximumEntryPayloadBytes));
    }

    [Fact]
    public void With_WhenCalledWithoutChanges_ClonesEveryField()
    {
        var settings = SqliteSessionStoreSettings.CreateDefault();

        var cloned = settings with { };

        cloned.ShouldNotBeSameAs(settings);
        cloned.ShouldBe(settings);
    }

    [Fact]
    public void Equals_WhenSnapshotBoundDiffers_IsNotEqual()
    {
        var left = new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), 1, 1);
        var right = new SqliteSessionStoreSettings(TimeSpan.FromSeconds(1), 1, 2);

        left.ShouldNotBe(right);
    }
}
