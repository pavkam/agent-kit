// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies immutable store bounds reject values the provider cannot represent or enforce.</summary>
public sealed class SqliteDurableStoreSettingsTests
{
    /// <summary>Verifies explicit valid bounds are retained exactly.</summary>
    [Fact]
    public void Constructor_WhenBoundsAreValid_RetainsThemExactly()
    {
        var settings = new SqliteDurableStoreSettings(TimeSpan.FromSeconds(3), 2048);

        settings.LockTimeout.ShouldBe(TimeSpan.FromSeconds(3));
        settings.MaximumRecordBytes.ShouldBe(2048);
    }

    /// <summary>Verifies the documented defaults are conservative and positive.</summary>
    [Fact]
    public void CreateDefault_WhenCalled_ReturnsTheDocumentedConservativeBounds()
    {
        var settings = SqliteDurableStoreSettings.CreateDefault();

        settings.LockTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        settings.MaximumRecordBytes.ShouldBe(1_048_576);
    }

    /// <summary>Verifies a sub-second timeout is refused, because the provider's timeout is whole seconds.</summary>
    [Fact]
    public void Constructor_WhenLockTimeoutIsBelowOneSecond_ThrowsForTheLockTimeoutArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SqliteDurableStoreSettings(TimeSpan.FromMilliseconds(500), 2048));

        exception.ParamName.ShouldBe("lockTimeout");
    }

    /// <summary>Verifies a fractional-second timeout is refused rather than silently truncated.</summary>
    [Fact]
    public void Constructor_WhenLockTimeoutIsNotWholeSeconds_ThrowsForTheLockTimeoutArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SqliteDurableStoreSettings(TimeSpan.FromMilliseconds(1500), 2048));

        exception.ParamName.ShouldBe("lockTimeout");
    }

    /// <summary>Verifies a nonpositive record bound is refused, because no payload could ever satisfy it.</summary>
    [Fact]
    public void Constructor_WhenMaximumRecordBytesIsZero_ThrowsForThatArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SqliteDurableStoreSettings(TimeSpan.FromSeconds(1), 0));

        exception.ParamName.ShouldBe("maximumRecordBytes");
    }
}
