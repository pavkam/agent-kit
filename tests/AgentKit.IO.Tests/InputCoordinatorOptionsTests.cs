// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class InputCoordinatorOptionsTests
{
    [Fact]
    public void Constructor_WhenValuesAreOmitted_SelectsDocumentedPackageDefaults()
    {
        var options = new InputCoordinatorOptions();

        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(1));
        options.MaximumInputParts.ShouldBe(256);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenPartBoundIsNotPositive_RejectsExactArgument(int parts) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InputCoordinatorOptions(maximumInputParts: parts))
            .ParamName.ShouldBe("maximumInputParts");

    [Fact]
    public void Constructor_WhenValuesAreSupplied_RetainsThemExactly()
    {
        var options = new InputCoordinatorOptions(new ConfigurationVersion(9), 4);

        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(9));
        options.MaximumInputParts.ShouldBe(4);
    }

    [Fact]
    public void Constructor_WhenPreprocessingRevisionIsDefault_RejectsExactArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InputCoordinatorOptions(default(ConfigurationVersion)))
            .ParamName.ShouldBe("preprocessingConfigurationVersion");

    [Fact]
    public void Properties_WhenAssignedAfterConstruction_RetainTheAssignedValues()
    {
        var options = new InputCoordinatorOptions
        {
            PreprocessingConfigurationVersion = new ConfigurationVersion(5),
            MaximumInputParts = 12,
        };

        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(5));
        options.MaximumInputParts.ShouldBe(12);
    }

    [Fact]
    public void Properties_WhenAssignedAfterValidatingConstruction_OverwriteTheConstructedValues()
    {
        var options = new InputCoordinatorOptions(new ConfigurationVersion(9), 4)
        {
            MaximumInputParts = 7,
        };

        options.PreprocessingConfigurationVersion.ShouldBe(new ConfigurationVersion(9));
        options.MaximumInputParts.ShouldBe(7);
    }

    [Fact]
    public void Equals_WhenValuesMatch_UsesReferenceIdentityBecauseTheTypeIsMutable()
    {
        var left = new InputCoordinatorOptions(new ConfigurationVersion(3), 8);
        var right = new InputCoordinatorOptions(new ConfigurationVersion(3), 8);

        left.ShouldNotBe(right);
        left.ShouldBe(left);
    }
}
