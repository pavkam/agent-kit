// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Collections.Immutable;
using System.Reflection;

using AgentKit;

public sealed class ToolResultProjectionPolicyContractsTests
{
    [Fact]
    public void PolicyKey_WhenTextIsNull_ThrowsExactArgumentNullExceptionAndParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyKey(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void PolicyKey_WhenTextIsBlank_ThrowsExactArgumentExceptionAndParameterName(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultProjectionPolicyKey(value));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void PolicyKey_WhenTextIsValid_RetainsTextAndUsesStructuralEquality()
    {
        var first = new ToolResultProjectionPolicyKey("projection.default");
        var second = new ToolResultProjectionPolicyKey("projection.default");

        first.Value.ShouldBe("projection.default");
        first.ToString().ShouldBe("projection.default");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void PolicyVersion_WhenValueIsNotPositive_ThrowsExactParameterName(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionPolicyVersion(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void PolicyVersion_WhenValueIsPositive_RetainsInvariantText()
    {
        var version = new ToolResultProjectionPolicyVersion(7);

        version.Value.ShouldBe(7);
        version.ToString().ShouldBe("7");
    }

    [Fact]
    public void PolicyVersion_WhenValueIsMaximum_RetainsValue()
    {
        var version = new ToolResultProjectionPolicyVersion(long.MaxValue);

        version.Value.ShouldBe(long.MaxValue);
    }

    [Fact]
    public void Reference_WhenKeyOrVersionIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyReference(default, Version()))
            .ParamName.ShouldBe("key");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionPolicyReference(Key(), default))
            .ParamName.ShouldBe("version");
    }

    [Fact]
    public void Reference_WhenValid_RetainsImmutableIdentity()
    {
        var reference = Reference();

        reference.Key.ShouldBe(Key());
        reference.Version.ShouldBe(Version());
        reference.ShouldBe(new ToolResultProjectionPolicyReference(Key(), Version()));
        reference.GetHashCode().ShouldBe(new ToolResultProjectionPolicyReference(Key(), Version()).GetHashCode());
    }

    [Theory]
    [InlineData(0L, 1, "maximumBytes")]
    [InlineData(-1L, 1, "maximumBytes")]
    [InlineData(1L, 0, "maximumParts")]
    [InlineData(1L, -1, "maximumParts")]
    public void Bounds_WhenValueIsNotPositive_ThrowsExactParameterName(long maximumBytes, int maximumParts, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolResultProjectionBounds(maximumBytes, maximumParts));

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
    public void Snapshot_WhenRequiredReferenceIsNull_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultProjectionPolicySnapshot(null!, Bounds(), ToolResultProjectionTransformations.None, ExtensionData.Empty));

        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Snapshot_WhenRequiredBoundsOrExtensionsAreNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(
                () => new ToolResultProjectionPolicySnapshot(Reference(), null!, ToolResultProjectionTransformations.None, ExtensionData.Empty))
            .ParamName.ShouldBe("bounds");
        Should.Throw<ArgumentNullException>(
                () => new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), ToolResultProjectionTransformations.None, null!))
            .ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Snapshot_WhenCopiedExtensionBagHasNullValues_ThrowsExactParameterName()
    {
        var malformed = ExtensionData.Empty with { Values = null! };

        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicySnapshot(
            Reference(), Bounds(), ToolResultProjectionTransformations.None, malformed));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Snapshot_WhenCopiedExtensionBagHasDefaultValueBuffer_ThrowsExactParameterName()
    {
        var malformed = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("future", default));

        var exception = Should.Throw<ArgumentException>(() => new ToolResultProjectionPolicySnapshot(
            Reference(), Bounds(), ToolResultProjectionTransformations.None, malformed));

        exception.ParamName.ShouldBe("extensions");
    }

    [Theory]
    [InlineData(32)]
    [InlineData(-1)]
    public void Snapshot_WhenTransformationsContainUnknownBits_ThrowsExactParameterName(int rawValue)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionPolicySnapshot(
            Reference(), Bounds(), (ToolResultProjectionTransformations) rawValue, ExtensionData.Empty));

        exception.ParamName.ShouldBe("allowedTransformations");
    }

    [Fact]
    public void Snapshot_WhenTransformationsAreKnownCombinations_RetainsEveryImmutableField()
    {
        var transformations = ToolResultProjectionTransformations.Redaction
            | ToolResultProjectionTransformations.Normalization
            | ToolResultProjectionTransformations.Summarization
            | ToolResultProjectionTransformations.Truncation
            | ToolResultProjectionTransformations.Externalization;
        var snapshot = new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), transformations, ExtensionData.Empty);

        snapshot.Reference.ShouldBe(Reference());
        snapshot.Bounds.ShouldBe(Bounds());
        snapshot.AllowedTransformations.ShouldBe(transformations);
        snapshot.Extensions.ShouldBeSameAs(ExtensionData.Empty);
        snapshot.ShouldBe(new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), transformations, ExtensionData.Empty));
        snapshot.GetHashCode().ShouldBe(new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), transformations, ExtensionData.Empty).GetHashCode());
    }

    [Fact]
    public void Snapshot_WhenEachKnownTransformationCombinationIsUsed_AcceptsIt()
    {
        for (var rawValue = 0; rawValue <= 31; rawValue++)
        {
            var transformations = (ToolResultProjectionTransformations) rawValue;

            _ = Should.NotThrow(() => new ToolResultProjectionPolicySnapshot(
                Reference(), Bounds(), transformations, ExtensionData.Empty));
        }
    }

    [Fact]
    public void Snapshot_WhenExtensionMapsAreIndependentlyConstructedWithSameContent_IsStructurallyEqual()
    {
        var firstExtensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("future", new ExtensionValue([1, 2, 3])));
        var secondExtensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("future", new ExtensionValue([1, 2, 3])));
        var first = new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), ToolResultProjectionTransformations.None, firstExtensions);
        var second = new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), ToolResultProjectionTransformations.None, secondExtensions);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void PolicyRecords_WhenInspected_ExposeGetOnlyProperties()
    {
        typeof(ToolResultProjectionPolicyReference).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ShouldAllBe(static property => property.SetMethod == null);
        typeof(ToolResultProjectionBounds).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ShouldAllBe(static property => property.SetMethod == null);
        typeof(ToolResultProjectionPolicySnapshot).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ShouldAllBe(static property => property.SetMethod == null);
    }

    private static ToolResultProjectionPolicyKey Key() => new("projection.default");

    private static ToolResultProjectionPolicyVersion Version() => new(1);

    private static ToolResultProjectionPolicyReference Reference() => new(Key(), Version());

    private static ToolResultProjectionBounds Bounds() => new(1_024, 4);
}
