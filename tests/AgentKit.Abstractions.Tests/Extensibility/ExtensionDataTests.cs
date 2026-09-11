// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Extensibility;

using AgentKit;

/// <summary>Verifies ExtensionData behavior and contracts.</summary>
public sealed class ExtensionDataTests
{
    [Fact]
    public void Constructor_WhenValuesIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExtensionData(null!));
        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void Empty_WhenAccessed_HasNoValues() => ExtensionData.Empty.Values.ShouldBeEmpty();
    [Fact]
    public void Empty_WhenAccessedTwice_ReturnsSameInstance() => // Documented as a shared instance to avoid needless allocation on
    // the common "no extensions" path.
    ReferenceEquals(ExtensionData.Empty, ExtensionData.Empty).ShouldBeTrue();
    [Fact]
    public void Constructor_WhenValuesArePopulated_RoundTrips()
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty.Add("safety", new ExtensionValue([1, 2, 3]));
        var data = new ExtensionData(values);
        data.Values.ShouldContainKey("safety");
    }

    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreEqual()
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([9]));
        var first = new ExtensionData(values);
        var second = new ExtensionData(values);
        first.ShouldBe(second);
    }

    [Fact]
    public void Equals_WhenOtherIsNull_ReturnsFalse() => ExtensionData.Empty.Equals(null).ShouldBeFalse();
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
        var second = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("k", new ExtensionValue([1])).Add("k2", new ExtensionValue([2])));
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
        var first = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("a", new ExtensionValue([1])).Add("b", new ExtensionValue([2])));
        var second = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("b", new ExtensionValue([2])).Add("a", new ExtensionValue([1])));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Constructor_WhenSourceUsesNonOrdinalComparer_NormalizesOwnershipButPreservesKeySpelling()
    {
        var source = ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase).Add("Future.Field", new ExtensionValue([1]));
        var data = new ExtensionData(source);
        data.Values.KeyComparer.ShouldBe(StringComparer.Ordinal);
        data.Values.ValueComparer.ShouldBe(EqualityComparer<ExtensionValue>.Default);
        data.Values.Keys.ShouldBe(["Future.Field"]);
        data.Values["Future.Field"].ShouldBe(new ExtensionValue([1]));
        data.Values.ContainsKey("future.field").ShouldBeFalse();
    }

    [Fact]
    public void Equality_WhenEquivalentBagsUseDifferentSourceComparers_IsSymmetricTransitiveAndHashConsistent()
    {
        var ordinal = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.Ordinal).Add("Future.Field", new ExtensionValue([1, 2])));
        var ignoreCase = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase).Add("Future.Field", new ExtensionValue([1, 2])));
        var copied = ignoreCase with
        {
        };
        ordinal.Equals(ignoreCase).ShouldBeTrue();
        ignoreCase.Equals(ordinal).ShouldBeTrue();
        ordinal.Equals(copied).ShouldBeTrue();
        ignoreCase.Equals(copied).ShouldBeTrue();
        ordinal.GetHashCode().ShouldBe(ignoreCase.GetHashCode());
        ignoreCase.GetHashCode().ShouldBe(copied.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOnlyKeyCaseDiffers_RemainsSymmetricAndDistinct()
    {
        var upper = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase).Add("Future.Field", new ExtensionValue([1])));
        var lower = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.Ordinal).Add("future.field", new ExtensionValue([1])));
        upper.Equals(lower).ShouldBeFalse();
        lower.Equals(upper).ShouldBeFalse();
    }

    [Fact]
    public void ValuesInitializer_WhenNull_ThrowsBeforeCopyIsCreated()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ExtensionData.Empty with { Values = null! });
        exception.ParamName.ShouldBe(nameof(ExtensionData.Values));
    }

    [Fact]
    public void Constructor_WhenSourceHasCustomValueComparer_NormalizesFutureSetItemComparison()
    {
        var source = ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase, EqualityComparer<ExtensionValue>.Create(static (_, _) => true)).Add("future", new ExtensionValue([1]));
        var data = new ExtensionData(source);
        var changed = data.Values.SetItem("future", new ExtensionValue([2]));
        changed["future"].ShouldBe(new ExtensionValue([2]));
        changed.KeyComparer.ShouldBe(StringComparer.Ordinal);
        changed.ValueComparer.ShouldBe(EqualityComparer<ExtensionValue>.Default);
    }

    [Fact]
    public void ValuesInitializer_WhenCopiedFromCustomComparer_NormalizesBothComparers()
    {
        var source = ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase, EqualityComparer<ExtensionValue>.Create(static (_, _) => true)).Add("Future", new ExtensionValue([1]));
        var original = new ExtensionData(source);
        var copy = original with
        {
            Values = source
        };
        copy.Values.KeyComparer.ShouldBe(StringComparer.Ordinal);
        copy.Values.ValueComparer.ShouldBe(EqualityComparer<ExtensionValue>.Default);
        copy.Values.Keys.ShouldBe(["Future"]);
    }

    [Fact]
    public void Constructor_WhenValuesAreNull_ThrowsOriginalParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExtensionData(null!));
        exception.ParamName.ShouldBe("values");
    }
}
