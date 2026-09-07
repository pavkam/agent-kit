// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Extensibility;

using AgentKit;

public sealed class ExtensionDataTests
{
    [Fact]
    public void Constructor_WhenValuesIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ExtensionData(null!));

        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void Empty_WhenAccessed_HasNoValues() => ExtensionData.Empty.Values.ShouldBeEmpty();

    [Fact]
    public void Empty_WhenAccessedTwice_ReturnsSameInstance() =>
        // Documented as a shared instance to avoid needless allocation on
        // the common "no extensions" path.
        ReferenceEquals(ExtensionData.Empty, ExtensionData.Empty).ShouldBeTrue();

    [Fact]
    public void Constructor_WhenValuesArePopulated_RoundTrips()
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("safety", new ExtensionValue([1, 2, 3]));

        var data = new ExtensionData(values);

        data.Values.ShouldContainKey("safety");
    }

    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreEqual()
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("k", new ExtensionValue([9]));

        var first = new ExtensionData(values);
        var second = new ExtensionData(values);

        first.ShouldBe(second);
    }

    [Fact]
    public void Equals_WhenOtherIsNull_ReturnsFalse() =>
        ExtensionData.Empty.Equals(null).ShouldBeFalse();

    [Fact]
    public void Equals_WhenSameReference_ReturnsTrue()
    {
        var data = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([1])));

        data.Equals(data).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WhenCountDiffers_ReturnsFalse()
    {
        var first = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([1])));
        var second = new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([1])).Add("k2", new ExtensionValue([2])));

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenKeyMissingInOther_ReturnsFalse()
    {
        var first = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([1])));
        var second = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("other", new ExtensionValue([1])));

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenValueDiffersForSameKey_ReturnsFalse()
    {
        var first = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([1])));
        var second = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([2])));

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_WhenInsertionOrderDiffers_ProducesSameHashCode()
    {
        var first = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("a", new ExtensionValue([1]))
            .Add("b", new ExtensionValue([2])));
        var second = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("b", new ExtensionValue([2]))
            .Add("a", new ExtensionValue([1])));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
