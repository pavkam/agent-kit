// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using System.Globalization;
using System.Numerics;

using AgentKit;

/// <summary>Verifies BudgetQuantity behavior and contracts.</summary>
public sealed class BudgetQuantityTests
{
    [Fact]
    public void Constructor_WhenZeroOrTrailingZeros_NormalizesCanonically()
    {
        default(BudgetQuantity).ShouldBe(new BudgetQuantity(0, 0));
        new BudgetQuantity(1200, 2).ShouldBe(new BudgetQuantity(12, 0));
        default(BudgetQuantity).GetHashCode().ShouldBe(new BudgetQuantity(BigInteger.Zero, 28).GetHashCode());
        new BudgetQuantity(1200, 2).GetHashCode().ShouldBe(new BudgetQuantity(12, 0).GetHashCode());
    }

    [Fact]
    public void Add_WhenDecimalMaximumPlusOne_PreservesExactQuantity() => BudgetQuantity.FromDecimal(decimal.MaxValue).Add(new BudgetQuantity(1, 0)).ToString().ShouldBe("79228162514264337593543950336");
    [Fact]
    public void Add_WhenScalesDiffer_PreservesFractionalQuantityExactly()
    {
        var result = new BudgetQuantity(12, 1).Add(new BudgetQuantity(34, 4));
        result.ShouldBe(new BudgetQuantity(12034, 4));
        result.ToString().ShouldBe("1.2034");
    }

    [Fact]
    public void CompareTo_WhenScalesDiffer_UsesExactNumericOrdering()
    {
        var smaller = new BudgetQuantity(11999, 4);
        var larger = new BudgetQuantity(12, 1);
        smaller.CompareTo(larger).ShouldBeLessThan(0);
        larger.CompareTo(smaller).ShouldBeGreaterThan(0);
        new BudgetQuantity(1200, 3).CompareTo(larger).ShouldBe(0);
        (smaller < larger).ShouldBeTrue();
        (smaller <= larger).ShouldBeTrue();
        (larger > smaller).ShouldBeTrue();
        (larger >= smaller).ShouldBeTrue();
    }

    [Fact]
    public void FromDecimal_WhenValueIsNegative_ThrowsWithExactParameterName() => Should.Throw<ArgumentOutOfRangeException>(() => BudgetQuantity.FromDecimal(-0.0000000000000000000000000001m)).ParamName.ShouldBe("value");
    [Theory]
    [InlineData("0")]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("79228162514264337593543950335")]
    public void TryGetDecimal_WhenExactDecimalBoundaryIsRepresentable_ReturnsExactValue(string text)
    {
        var expected = decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
        var quantity = BudgetQuantity.FromDecimal(expected);
        quantity.TryGetDecimal(out var actual).ShouldBeTrue();
        actual.ShouldBe(expected);
        quantity.ToDecimalChecked().ShouldBe(expected);
    }

    [Fact]
    public void TryGetDecimal_WhenMagnitudeIsNotRepresentable_ReturnsFalseAndZero()
    {
        var quantity = new BudgetQuantity(DecimalMaximumCoefficient + BigInteger.One, 0);
        quantity.TryGetDecimal(out var value).ShouldBeFalse();
        value.ShouldBe(decimal.Zero);
    }

    [Fact]
    public void TryGetDecimal_WhenPrecisionIsNotRepresentable_ReturnsFalseAndZero()
    {
        var quantity = new BudgetQuantity(DecimalMaximumCoefficient + BigInteger.One, 28);
        quantity.TryGetDecimal(out var value).ShouldBeFalse();
        value.ShouldBe(decimal.Zero);
    }

    [Fact]
    public void ToDecimalChecked_WhenQuantityIsNotRepresentable_ThrowsOverflowException() => Should.Throw<OverflowException>(() => new BudgetQuantity(DecimalMaximumCoefficient + BigInteger.One, 28).ToDecimalChecked());
    [Fact]
    public void ToString_WhenCurrentCultureUsesDifferentDigitsOrSeparator_RemainsInvariant()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
            new BudgetQuantity(1234, 3).ToString().ShouldBe("1.234");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData(-1, 0, "coefficient")]
    [InlineData(1, -1, "scale")]
    [InlineData(1, 29, "scale")]
    public void Constructor_WhenInvalid_ThrowsArgumentOutOfRangeException(int coefficient, int scale, string parameterName) => Should.Throw<ArgumentOutOfRangeException>(() => new BudgetQuantity(coefficient, scale)).ParamName.ShouldBe(parameterName);
    [Fact]
    public void ExactQuantityConstructors_WhenValuesExceedDecimal_PreserveExactValues()
    {
        var exact = new BudgetQuantity(BigInteger.One << 200, 28);
        var usage = new BudgetDimensionUsage(new BudgetDimension("tests.tokens"), new BudgetUnit("tokens"), exact, exact.Add(exact), null);
        var failure = new BudgetLimitFailure(new BudgetScopeId(Guid.NewGuid()), new BudgetDimension("tests.tokens"), BudgetLimitKind.Hard, decimal.MaxValue, exact, exact.Add(exact), new BudgetUnit("tokens"), "limit exceeded");
        usage.Reserved.ShouldBe(exact);
        usage.Committed.ShouldBe(exact.Add(exact));
        failure.ObservedValue.ShouldBe(exact);
        failure.RequestedAmount.ShouldBe(exact.Add(exact));
    }

    private static BigInteger DecimalMaximumCoefficient => (BigInteger.One << 96) - BigInteger.One;
}
