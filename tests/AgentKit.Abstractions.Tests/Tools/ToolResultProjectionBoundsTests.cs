// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Reflection;

using AgentKit;

/// <summary>Verifies ToolResultProjectionBounds behavior and contracts.</summary>
public sealed class ToolResultProjectionBoundsTests
{
    [Theory]
    [InlineData(0L, 1, "maximumBytes")]
    [InlineData(-1L, 1, "maximumBytes")]
    [InlineData(1L, 0, "maximumParts")]
    [InlineData(1L, -1, "maximumParts")]
    public void Bounds_WhenValueIsNotPositive_ThrowsExactParameterName(long maximumBytes, int maximumParts, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionBounds(maximumBytes, maximumParts));
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Bounds_WhenAtPositiveBoundary_RetainsBothFiniteLimits()
    {
        var bounds = new ToolResultProjectionBounds(1, 1);
        bounds.MaximumBytes.ShouldBe(1);
        bounds.MaximumParts.ShouldBe(1);
        bounds.ShouldBe(new ToolResultProjectionBounds(1, 1));
    }

    [Fact]
    public void Bounds_WhenAtMaximumValues_RetainsBothFiniteLimits()
    {
        var bounds = new ToolResultProjectionBounds(long.MaxValue, int.MaxValue);
        bounds.MaximumBytes.ShouldBe(long.MaxValue);
        bounds.MaximumParts.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void PolicyRecords_WhenInspected_ExposeGetOnlyProperties() => typeof(ToolResultProjectionBounds).GetProperties(BindingFlags.Instance | BindingFlags.Public).ShouldAllBe(static property => property.SetMethod == null);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolResultProjectionBounds(1, 1);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
