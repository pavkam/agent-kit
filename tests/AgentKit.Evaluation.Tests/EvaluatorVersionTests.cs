// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluatorVersionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Constructor_WhenValueIsNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new EvaluatorVersion(value));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WhenValueIsPositive_PreservesIt(long value) =>
        new EvaluatorVersion(value).Value.ShouldBe(value);

    [Fact]
    public void ToString_WhenCultureUsesOtherDigits_UsesInvariantDecimalText()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");

            new EvaluatorVersion(1234).ToString().ShouldBe("1234");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
