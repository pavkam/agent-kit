// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

public sealed class ComponentRegistrationArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfNotClosedType_WhenTypeIsOpenGeneric_ThrowsWithInferredParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotClosedType(typeof(List<>)));

        exception.ParamName.ShouldBe("typeof(List<>)");
    }

    [Fact]
    public void ThrowIfNotConcreteClosedType_WhenTypeIsInterface_ThrowsWithInferredParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotConcreteClosedType(typeof(IDisposable)));

        exception.ParamName.ShouldBe("typeof(IDisposable)");
    }

    [Fact]
    public void ThrowIfNotAssignableTo_WhenImplementationDoesNotImplementContract_ThrowsWithInferredParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotAssignableTo(typeof(string), typeof(IDisposable)));

        exception.ParamName.ShouldBe("typeof(string)");
    }

    [Fact]
    public void ThrowIfNotDisposalContract_WhenContractIsIDisposable_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfNotDisposalContract(typeof(IDisposable)));
}
