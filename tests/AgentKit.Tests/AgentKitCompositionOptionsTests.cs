// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class AgentKitCompositionOptionsTests
{
    [Fact]
    public void Constructor_WhenDefaultIsUsed_PreservesDocumentedBound()
    {
        var options = new AgentKitCompositionOptions();

        options.MaximumDerivedInfrastructureRegistrations.ShouldBe(
            AgentKitCompositionOptions.DefaultMaximumDerivedInfrastructureRegistrations);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_WhenBoundIsAtSupportedBoundary_PreservesValue(int value)
    {
        var options = new AgentKitCompositionOptions(value);

        options.MaximumDerivedInfrastructureRegistrations.ShouldBe(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_WhenBoundIsOutsideSupportedRange_ThrowsExactArgumentOutOfRangeException(int value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new AgentKitCompositionOptions(value));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("maximumDerivedInfrastructureRegistrations");
    }
}
