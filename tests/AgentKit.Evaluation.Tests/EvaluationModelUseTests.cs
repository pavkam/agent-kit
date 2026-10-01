// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationModelUseTests
{
    [Theory]
    [InlineData(null, "f", "m", "provider")]
    [InlineData("p", " ", "m", "apiFamily")]
    [InlineData("p", "f", "", "model")]
    public void Constructor_WhenRequiredTextIsBlank_ThrowsWithTheParameterName(string? provider, string? family, string? model, string parameter) =>
        Should.Throw<ArgumentException>(() => new EvaluationModelUse(provider!, family!, model!, null)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenDeploymentIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationModelUse("p", "f", "m", " ")).ParamName.ShouldBe("deployment");

    [Fact]
    public void Constructor_WhenDeploymentIsAbsent_LeavesItNull() =>
        new EvaluationModelUse("p", "f", "m", null).Deployment.ShouldBeNull();

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesEveryValue()
    {
        var use = new EvaluationModelUse("p", "f", "m", "d");

        (use.Provider, use.ApiFamily, use.Model, use.Deployment).ShouldBe(("p", "f", "m", "d"));
    }
}
