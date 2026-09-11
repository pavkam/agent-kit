// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Collections.Immutable;
using System.Reflection;

using AgentKit;

/// <summary>Verifies ToolResultProjectionPolicySnapshot behavior and contracts.</summary>
public sealed class ToolResultProjectionPolicySnapshotTests
{
    [Fact]
    public void Snapshot_WhenRequiredReferenceIsNull_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicySnapshot(null!, Bounds(), ToolResultProjectionTransformations.None, ExtensionData.Empty));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Snapshot_WhenRequiredBoundsOrExtensionsAreNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicySnapshot(Reference(), null!, ToolResultProjectionTransformations.None, ExtensionData.Empty)).ParamName.ShouldBe("bounds");
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), ToolResultProjectionTransformations.None, null!)).ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Snapshot_WhenExtensionBagCopyHasNullValues_RejectsAtExtensionBoundary()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ExtensionData.Empty with { Values = null! });
        exception.ParamName.ShouldBe(nameof(ExtensionData.Values));
    }

    [Fact]
    public void Snapshot_WhenCopiedExtensionBagHasDefaultValueBuffer_ThrowsExactParameterName()
    {
        var malformed = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("future", default));
        var exception = Should.Throw<ArgumentException>(() => new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), ToolResultProjectionTransformations.None, malformed));
        exception.ParamName.ShouldBe("extensions");
    }

    [Theory]
    [InlineData(32)]
    [InlineData(-1)]
    public void Snapshot_WhenTransformationsContainUnknownBits_ThrowsExactParameterName(int rawValue)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), (ToolResultProjectionTransformations) rawValue, ExtensionData.Empty));
        exception.ParamName.ShouldBe("allowedTransformations");
    }

    [Fact]
    public void Snapshot_WhenTransformationsAreKnownCombinations_RetainsEveryImmutableField()
    {
        var transformations = ToolResultProjectionTransformations.Redaction | ToolResultProjectionTransformations.Normalization | ToolResultProjectionTransformations.Summarization | ToolResultProjectionTransformations.Truncation | ToolResultProjectionTransformations.Externalization;
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
            _ = Should.NotThrow(() => new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), transformations, ExtensionData.Empty));
        }
    }

    [Fact]
    public void Snapshot_WhenExtensionMapsAreIndependentlyConstructedWithSameContent_IsStructurallyEqual()
    {
        var firstExtensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("future", new ExtensionValue([1, 2, 3])));
        var secondExtensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("future", new ExtensionValue([1, 2, 3])));
        var first = new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), ToolResultProjectionTransformations.None, firstExtensions);
        var second = new ToolResultProjectionPolicySnapshot(Reference(), Bounds(), ToolResultProjectionTransformations.None, secondExtensions);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static ToolResultProjectionPolicyKey Key() => new("projection.default");
    private static ToolResultProjectionPolicyVersion Version() => new(1);
    private static ToolResultProjectionPolicyReference Reference() => new(Key(), Version());
    private static ToolResultProjectionBounds Bounds() => new(1_024, 4);
    [Fact]
    public void PolicyRecords_WhenInspected_ExposeGetOnlyProperties() => typeof(ToolResultProjectionPolicySnapshot).GetProperties(BindingFlags.Instance | BindingFlags.Public).ShouldAllBe(static property => property.SetMethod == null);
}
