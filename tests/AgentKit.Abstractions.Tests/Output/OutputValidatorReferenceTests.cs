// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputValidatorReference behavior and contracts.</summary>
public sealed class OutputValidatorReferenceTests
{
    [Fact]
    public void Constructor_WhenNameIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new OutputValidatorReference(" ")).ParamName.ShouldBe("name");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsName()
    {
        var reference = new OutputValidatorReference("semantic");
        reference.Name.ShouldBe("semantic");
    }

    [Fact]
    public void ToString_WhenCalled_ReturnsName() =>
        new OutputValidatorReference("semantic").ToString().ShouldBe("semantic");
}
