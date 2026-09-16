// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;



/// <summary>Verifies SqliteSecurityGrantStoreSettings behavior and contracts.</summary>
public sealed class SqliteSecurityGrantStoreSettingsTests
{
    /// <summary>Verifies timeout and evidence bounds reject every unsupported boundary with the caller property name.</summary>
    [Theory]
    [InlineData(0, 1, 1, 1, 1, 1, "lockTimeout")]
    [InlineData(1, 0, 1, 1, 1, 1, "maximumGrantBytes")]
    [InlineData(1, 1, 0, 1, 1, 1, "maximumEnforcementBytes")]
    [InlineData(1, 1, 1, 0, 1, 1, "maximumResources")]
    [InlineData(1, 1, 1, 1, 0, 1, "maximumClaims")]
    [InlineData(1, 1, 1, 1, 1, 0, "maximumDelegationLinks")]
    public void Constructor_WhenSettingsAreInvalid_ThrowsExactArgument(int seconds, int grantBytes, int enforcementBytes, int resources, int claims, int links, string paramName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreSettings(TimeSpan.FromSeconds(seconds), grantBytes, enforcementBytes, resources, claims, links));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(paramName);
    }

    /// <summary>Verifies provider timeout representation rejects fractional and excessive durations.</summary>
    [Fact]
    public void Constructor_WhenTimeoutCannotBeRepresented_ThrowsExactArgument()
    {
        var fractional = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreSettings(TimeSpan.FromMilliseconds(1500), 1, 1, 1, 1, 1));
        var excessive = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreSettings(TimeSpan.MaxValue, 1, 1, 1, 1, 1));
        fractional.ParamName.ShouldBe("lockTimeout");
        excessive.ParamName.ShouldBe("lockTimeout");
    }

    /// <summary>Verifies default settings expose conservative bounds and support non-destructive copying.</summary>
    [Fact]
    public void CreateDefault_WhenCopiedWithNoChanges_RetainsEveryBound()
    {
        var original = SqliteSecurityGrantStoreSettings.CreateDefault();

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
    }
}
