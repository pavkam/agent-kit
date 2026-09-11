// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

using AgentKit;

/// <summary>Verifies ContextContributorCatalogVersion behavior and contracts.</summary>
public sealed class ContextContributorCatalogVersionTests: Conformance.LongIdentityConformanceTests<ContextContributorCatalogVersion>
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void ContextContributorCatalogVersion_Constructor_WhenNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ContextContributorCatalogVersion(value));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ContextContributorCatalogVersion_Constructor_WhenPositive_PreservesBoundaryEqualityAndInvariantFormatting()
    {
        var version = new ContextContributorCatalogVersion(long.MaxValue);
        var same = new ContextContributorCatalogVersion(long.MaxValue);
        version.Value.ShouldBe(long.MaxValue);
        version.ShouldBe(same);
        version.GetHashCode().ShouldBe(same.GetHashCode());
        version.ToString().ShouldBe("9223372036854775807");
        default(ContextContributorCatalogVersion).Value.ShouldBe(0);
        default(ContextContributorCatalogVersion).ToString().ShouldBe("0");
    }

    /// <inheritdoc/>
    protected override ContextContributorCatalogVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ContextContributorCatalogVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
