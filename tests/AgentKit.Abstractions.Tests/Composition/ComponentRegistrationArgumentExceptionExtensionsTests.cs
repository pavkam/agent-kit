// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

public sealed class ComponentRegistrationArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfNotClosedType_WhenTypeIsValid_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfNotClosedType(typeof(string)));

    [Fact]
    public void ThrowIfNotClosedType_WhenTypeIsNullOrOpenGeneric_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotClosedType(nullType!));
        var openException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotClosedType(typeof(List<>)));

        nullException.ParamName.ShouldBe("nullType");
        openException.ParamName.ShouldBe("typeof(List<>)");
    }

    [Fact]
    public void ThrowIfNotConcreteClosedType_WhenTypeIsValid_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfNotConcreteClosedType(typeof(Implementation)));

    [Fact]
    public void ThrowIfNotConcreteClosedType_WhenTypeIsNullOrInterface_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotConcreteClosedType(nullType!));
        var interfaceException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotConcreteClosedType(typeof(IDisposable)));

        nullException.ParamName.ShouldBe("nullType");
        interfaceException.ParamName.ShouldBe("typeof(IDisposable)");
    }

    [Fact]
    public void ThrowIfNotComponentContractType_WhenTypeIsValid_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfNotComponentContractType(typeof(IContract)));

    [Fact]
    public void ThrowIfNotComponentContractType_WhenTypeIsNullOrValueType_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotComponentContractType(nullType!));
        var valueException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotComponentContractType(typeof(int)));

        nullException.ParamName.ShouldBe("nullType");
        valueException.ParamName.ShouldBe("typeof(int)");
    }

    [Fact]
    public void ThrowIfNotComponentImplementationType_WhenTypeIsValid_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfNotComponentImplementationType(typeof(Implementation)));

    [Fact]
    public void ThrowIfNotComponentImplementationType_WhenTypeIsNullOrVoid_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotComponentImplementationType(nullType!));
        var voidException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotComponentImplementationType(typeof(void)));

        nullException.ParamName.ShouldBe("nullType");
        voidException.ParamName.ShouldBe("typeof(void)");
    }

    [Fact]
    public void ThrowIfNotAssignableTo_WhenTypesAreAssignable_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfNotAssignableTo(typeof(Implementation), typeof(IContract)));

    [Fact]
    public void ThrowIfNotAssignableTo_WhenEitherTypeIsNullOrImplementationIsWrong_ThrowsExactExpectedException()
    {
        Type? nullImplementation = null;
        Type? nullContract = null;
        var implementationException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotAssignableTo(nullImplementation!, typeof(IContract)));
        var contractException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotAssignableTo(typeof(Implementation), nullContract!));
        var mismatchException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotAssignableTo(typeof(string), typeof(IDisposable)));

        implementationException.ParamName.ShouldBe("nullImplementation");
        contractException.ParamName.ShouldBe("contractType");
        mismatchException.ParamName.ShouldBe("typeof(string)");
    }

    [Fact]
    public void ThrowIfNotDisposalContract_WhenContractIsDisposable_DoesNotThrow()
    {
        Should.NotThrow(() => ArgumentException.ThrowIfNotDisposalContract(typeof(IDisposable)));
        Should.NotThrow(() => ArgumentException.ThrowIfNotDisposalContract(typeof(IAsyncDisposable)));
    }

    [Fact]
    public void ThrowIfNotDisposalContract_WhenContractIsNullOrWrong_ThrowsExactExpectedException()
    {
        Type? nullType = null;
        var nullException = ShouldThrowExactly<ArgumentNullException>(() => ArgumentException.ThrowIfNotDisposalContract(nullType!));
        var wrongException = ShouldThrowExactly<ArgumentException>(() => ArgumentException.ThrowIfNotDisposalContract(typeof(IContract)));

        nullException.ParamName.ShouldBe("nullType");
        wrongException.ParamName.ShouldBe("typeof(IContract)");
    }

    private static TException ShouldThrowExactly<TException>(Action action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(action);
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    private interface IContract;

    private sealed class Implementation: IContract;
}
