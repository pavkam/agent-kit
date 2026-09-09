// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Extensibility;

using AgentKit;

public sealed class ExtensionDataContractsTests
{
    [Fact]
    public void Constructor_WhenSourceUsesNonOrdinalComparer_NormalizesOwnershipButPreservesKeySpelling()
    {
        var source = ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase)
            .Add("Future.Field", new ExtensionValue([1]));

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
        var ordinal = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.Ordinal)
            .Add("Future.Field", new ExtensionValue([1, 2])));
        var ignoreCase = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase)
            .Add("Future.Field", new ExtensionValue([1, 2])));
        var copied = ignoreCase with { };

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
        var upper = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.OrdinalIgnoreCase)
            .Add("Future.Field", new ExtensionValue([1])));
        var lower = new ExtensionData(ImmutableDictionary.Create<string, ExtensionValue>(StringComparer.Ordinal)
            .Add("future.field", new ExtensionValue([1])));

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
        var source = ImmutableDictionary.Create<string, ExtensionValue>(
                StringComparer.OrdinalIgnoreCase,
                EqualityComparer<ExtensionValue>.Create(static (_, _) => true))
            .Add("future", new ExtensionValue([1]));
        var data = new ExtensionData(source);

        var changed = data.Values.SetItem("future", new ExtensionValue([2]));

        changed["future"].ShouldBe(new ExtensionValue([2]));
        changed.KeyComparer.ShouldBe(StringComparer.Ordinal);
        changed.ValueComparer.ShouldBe(EqualityComparer<ExtensionValue>.Default);
    }

    [Fact]
    public void ValuesInitializer_WhenCopiedFromCustomComparer_NormalizesBothComparers()
    {
        var source = ImmutableDictionary.Create<string, ExtensionValue>(
                StringComparer.OrdinalIgnoreCase,
                EqualityComparer<ExtensionValue>.Create(static (_, _) => true))
            .Add("Future", new ExtensionValue([1]));
        var original = new ExtensionData(source);

        var copy = original with { Values = source };

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
