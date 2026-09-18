// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the portable JSON mirror of one configured ceiling captured with a scope.</summary>
public sealed class JsonBudgetLimitTests
{
    /// <summary>Verifies projecting a null domain limit is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetLimit.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies a domain limit projects to a document carrying the unwrapped dimension, value, unit, and kind.</summary>
    [Fact]
    public void FromDomain_WhenValueIsValid_ProjectsExactFields()
    {
        var limit = new BudgetLimit(new BudgetDimension("tokens"), 10, new BudgetUnit("count"), BudgetLimitKind.Hard);

        var document = JsonBudgetLimit.FromDomain(limit);

        document.Dimension.ShouldBe("tokens");
        document.Value.ShouldBe(10);
        document.Unit.ShouldBe("count");
        document.Kind.ShouldBe(BudgetLimitKind.Hard);
    }

    /// <summary>Verifies a round trip through <see cref="JsonBudgetLimit.FromDomain"/> and <see cref="JsonBudgetLimit.ToDomain"/> reproduces an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_RoundTripsExactly()
    {
        var original = new BudgetLimit(new BudgetDimension("tokens"), 5, new BudgetUnit("bytes"), BudgetLimitKind.Soft);

        var restored = JsonBudgetLimit.FromDomain(original).ToDomain();

        restored.ShouldBe(original);
    }

    /// <summary>Verifies a blank persisted dimension is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenDimensionIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new JsonBudgetLimit(" ", 1, "count", BudgetLimitKind.Hard).ToDomain());

    /// <summary>Verifies a negative persisted value is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenValueIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetLimit("tokens", -1, "count", BudgetLimitKind.Hard).ToDomain());

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip under the canonical contract.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = new JsonBudgetLimit("tokens", 42, "count", BudgetLimitKind.Hard);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetLimit>(
            JsonStoreSerialization.Encode(original, options, 1_024), options);

        decoded.ShouldBe(original);
    }
}
