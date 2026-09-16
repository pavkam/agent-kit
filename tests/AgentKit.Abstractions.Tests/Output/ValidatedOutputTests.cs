// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using System.Text.Json;

/// <summary>Verifies ValidatedOutput behavior and contracts.</summary>
public sealed class ValidatedOutputTests
{
    [Fact]
    public void Constructor_WhenModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ValidatedOutput((OutputMode) 99, "text", null, null)).ParamName.ShouldBe("mode");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var output = new ValidatedOutput(OutputMode.Text, "text", null, null);
        output.Mode.ShouldBe(OutputMode.Text);
        output.Text.ShouldBe("text");
        output.Json.ShouldBeNull();
        output.Value.ShouldBeNull();
    }

    [Fact]
    public void Equality_WhenJsonValuesMatch_IsStructurallyEqual()
    {
        using var document = JsonDocument.Parse("{\"a\":1}");
        var left = new ValidatedOutput(OutputMode.NativeSchema, null, document.RootElement, null);
        var right = new ValidatedOutput(OutputMode.NativeSchema, null, document.RootElement, null);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOneJsonIsNull_IsNotEqual()
    {
        using var document = JsonDocument.Parse("{}");
        var left = new ValidatedOutput(OutputMode.NativeSchema, null, document.RootElement, null);
        var right = new ValidatedOutput(OutputMode.NativeSchema, null, null, null);
        left.ShouldNotBe(right);
    }

    [Fact]
    public void Equality_WhenBothJsonAreNull_IsEqual()
    {
        var left = new ValidatedOutput(OutputMode.Text, "text", null, null);
        var right = new ValidatedOutput(OutputMode.Text, "text", null, null);
        left.ShouldBe(right);
    }

    [Fact]
    public void Equality_WhenValueDiffers_IsNotEqual()
    {
        var left = new ValidatedOutput(OutputMode.Text, "text", null, "value");
        var right = new ValidatedOutput(OutputMode.Text, "text", null, "other");
        left.ShouldNotBe(right);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ValidatedOutput(OutputMode.Text, "text", null, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
