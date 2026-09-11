// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies BudgetProfileKey behavior and contracts.</summary>
public sealed class BudgetProfileKeyTests: Conformance.StringIdentityConformanceTests<BudgetProfileKey>
{
    /// <inheritdoc/>
    protected override BudgetProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(BudgetProfileKey subject) => subject.Value;
    [Fact]
    public void DefaultValues_HaveNoUsableSelectionText()
    {
        default(BudgetProfileKey).Value.ShouldBeNull();
        default(BudgetProfileKey).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenValueIsValid_RoundTripsWithStructuralEquality()
    {
        var first = new BudgetProfileKey("primary");
        var second = new BudgetProfileKey("primary");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldBe("primary");
        new BudgetProfileKey("secondary").ShouldNotBe(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetProfileKey(value!));
        _ = value is null ? exception.ShouldBeOfType<ArgumentNullException>() : exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_PreservesExactTextAndUsesOrdinalCaseSensitiveEquality()
    {
        var preserved = new BudgetProfileKey(" Primary ");
        preserved.ToString().ShouldBe(" Primary ");
        new BudgetProfileKey("primary").ShouldNotBe(new BudgetProfileKey("PRIMARY"));
    }
}
