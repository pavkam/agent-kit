// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

public sealed class ModelSelectionPolicyTests
{
    [Fact]
    public void Constructor_WhenCandidatesContainDefaultAlias_ThrowsArgumentOutOfRangeWithCandidates() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelSelectionPolicy([default])).ParamName.ShouldBe("candidates");

    [Fact]
    public void CandidatesInit_WhenCandidatesContainDefaultAlias_ThrowsArgumentOutOfRangeWithCandidates()
    {
        var policy = new ModelSelectionPolicy([new ModelAlias("chat")]);
        Should.Throw<ArgumentOutOfRangeException>(() => policy with { Candidates = [new ModelAlias("chat"), default] }).ParamName.ShouldBe("Candidates");
    }

    [Fact]
    public void Constructor_WhenCandidatesValid_PreservesCandidateOrder()
    {
        var policy = new ModelSelectionPolicy([new ModelAlias("a"), new ModelAlias("b")]);
        policy.Candidates.ShouldBe([new ModelAlias("a"), new ModelAlias("b")]);
    }

    [Fact]
    public void Constructor_WhenCandidatesAreDefaultOrEmpty_ThrowsExactArgumentException()
    {
        Should.Throw<ArgumentException>(() => new ModelSelectionPolicy(default)).ParamName.ShouldBe("candidates");
        Should.Throw<ArgumentException>(() => new ModelSelectionPolicy([])).ParamName.ShouldBe("candidates");
    }

    [Fact]
    public void Constructor_WhenCandidatesContainDuplicate_ThrowsExactArgumentException()
    {
        var alias = new ModelAlias("chat");
        var exception = Should.Throw<ArgumentException>(() => new ModelSelectionPolicy([alias, alias]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("candidates");
    }

    [Fact]
    public void Constructor_WhenFallbackIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelSelectionPolicy([new ModelAlias("chat")], (ModelFallbackPolicy) 99)).ParamName.ShouldBe("fallback");

    [Fact]
    public void Constructor_WhenDowngradeIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelSelectionPolicy([new ModelAlias("chat")], downgrade: (CapabilityDowngradePolicy) 99)).ParamName.ShouldBe("downgrade");

    [Fact]
    public void FallbackInit_WhenUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var policy = Policy();
        Should.Throw<ArgumentOutOfRangeException>(() => policy with { Fallback = (ModelFallbackPolicy) 99 }).ParamName.ShouldBe("Fallback");
    }

    [Fact]
    public void DowngradeInit_WhenUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var policy = Policy();
        Should.Throw<ArgumentOutOfRangeException>(() => policy with { Downgrade = (CapabilityDowngradePolicy) 99 }).ParamName.ShouldBe("Downgrade");
    }

    [Fact]
    public void ExtensionsInit_WhenNull_ThrowsExactArgumentNullException()
    {
        var policy = Policy();
        Should.Throw<ArgumentNullException>(() => policy with { Extensions = null! }).ParamName.ShouldBe("Extensions");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var policy = new ModelSelectionPolicy([new ModelAlias("chat")], ModelFallbackPolicy.OrderedCandidates, CapabilityDowngradePolicy.AllowDeclaredAdjustments);
        policy.Fallback.ShouldBe(ModelFallbackPolicy.OrderedCandidates);
        policy.Downgrade.ShouldBe(CapabilityDowngradePolicy.AllowDeclaredAdjustments);
        policy.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Equals_WhenCandidateOrderDiffers_IsNotEqual()
    {
        var first = new ModelSelectionPolicy([new ModelAlias("a"), new ModelAlias("b")]);
        var second = new ModelSelectionPolicy([new ModelAlias("b"), new ModelAlias("a")]);
        first.ShouldNotBe(second);
        first.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenEverythingMatches_IsEqualWithMatchingHashCode()
    {
        var first = Policy();
        var second = Policy();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Policy();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Initializer_WhenValuesAreValid_ReplaceExistingValues()
    {
        var original = Policy();
        var replacementCandidates = new[] { new ModelAlias("other") }.ToImmutableArray();
        var replacementExtensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("key", new ExtensionValue([1])));
        var copy = original with { Candidates = replacementCandidates, Extensions = replacementExtensions };
        copy.Candidates.ShouldBe(replacementCandidates);
        copy.Extensions.ShouldBe(replacementExtensions);
    }

    private static ModelSelectionPolicy Policy() => new([new ModelAlias("chat")]);
}
