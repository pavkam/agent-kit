// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

using Microsoft.Extensions.DependencyInjection;

public sealed class ComponentRegistrationDescriptorTests
{
    [Fact]
    public void Constructor_WhenLeafRegistrationIsValid_PreservesImmutableDescription()
    {
        var descriptor = new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(),
            typeof(Implementation),
            ServiceLifetime.Singleton,
            []);

        descriptor.Dependencies.ShouldBeEmpty();
        descriptor.ImplementationType.ShouldBe(typeof(Implementation));
    }

    [Fact]
    public void ContractReferenceConstructor_WhenContractIsNull_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ComponentContractReference(null!));

        exception.ParamName.ShouldBe("contractType");
    }

    [Fact]
    public void ContractReferenceConstructor_WhenContractIsOpenGeneric_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new ComponentContractReference(typeof(IGenericContract<>)));

        exception.ParamName.ShouldBe("contractType");
    }

    [Theory]
    [InlineData("void")]
    [InlineData("byref")]
    [InlineData("pointer")]
    [InlineData("value")]
    public void ContractReferenceConstructor_WhenContractCannotBeADiReferenceType_ThrowsWithParameterName(string shape)
    {
        var contractType = shape switch
        {
            "void" => typeof(void),
            "byref" => typeof(int).MakeByRefType(),
            "pointer" => typeof(int).MakePointerType(),
            _ => typeof(int),
        };

        var exception = Should.Throw<ArgumentException>(() => new ComponentContractReference(contractType));

        exception.ParamName.ShouldBe("contractType");
    }

    [Fact]
    public void ContractReferenceConstructor_WhenKeyIsBlank_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new ComponentContractReference(typeof(IContract), " "));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void From_WhenTypedKeyIsDefault_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => ComponentContractReference.From(default(ComponentKey<IContract>)));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void RegistrationConstructor_WhenImplementationIsNull_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), null!, ServiceLifetime.Singleton, []));

        exception.ParamName.ShouldBe("implementationType");
    }

    [Fact]
    public void RegistrationConstructor_WhenServiceIsNullOrLifetimeIsUndefined_ThrowsExactExpectedException()
    {
        var serviceException = Should.Throw<ArgumentNullException>(() => new ComponentRegistrationDescriptor(
            null!, typeof(Implementation), ServiceLifetime.Singleton, []));
        var lifetimeException = Should.Throw<ArgumentOutOfRangeException>(() => new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), typeof(Implementation), (ServiceLifetime) 99, []));

        serviceException.GetType().ShouldBe(typeof(ArgumentNullException));
        lifetimeException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        serviceException.ParamName.ShouldBe("service");
        lifetimeException.ParamName.ShouldBe("lifetime");
    }

    [Fact]
    public void RegistrationConstructor_WhenImplementationIsAbstractOrUnassignable_ThrowsWithParameterName()
    {
        var abstractException = Should.Throw<ArgumentException>(() => new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), typeof(AbstractImplementation), ServiceLifetime.Singleton, []));
        var unassignableException = Should.Throw<ArgumentException>(() => new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), typeof(UnrelatedImplementation), ServiceLifetime.Singleton, []));

        abstractException.ParamName.ShouldBe("implementationType");
        unassignableException.ParamName.ShouldBe("implementationType");
    }

    [Fact]
    public void RegistrationConstructor_WhenImplementationIsAnInterface_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), typeof(IContract), ServiceLifetime.Singleton, []));

        exception.ParamName.ShouldBe("implementationType");
    }

    [Fact]
    public void RegistrationConstructor_WhenDependenciesAreDefaultOrContainNull_ThrowsWithParameterName()
    {
        var defaultException = Should.Throw<ArgumentException>(() => new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), typeof(Implementation), ServiceLifetime.Singleton, default));
        var nullException = Should.Throw<ArgumentNullException>(() => new ComponentRegistrationDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), typeof(Implementation), ServiceLifetime.Singleton, [null!]));

        defaultException.ParamName.ShouldBe("dependencies");
        nullException.ParamName.ShouldBe("dependencies");
    }

    [Fact]
    public void DependencyConstructor_WhenCardinalityIsUndefined_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ComponentDependencyDescriptor(
            ComponentContractReference.Unkeyed<IContract>(), (ComponentDependencyCardinality) 99));

        exception.ParamName.ShouldBe("cardinality");
    }

    [Fact]
    public void DependencyConstructor_WhenReferenceIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ComponentDependencyDescriptor(
            null!, ComponentDependencyCardinality.RequiredSingular));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void DependencyConstructor_WhenCardinalityIsOptionalSingular_PreservesOptionalDependency()
    {
        var reference = ComponentContractReference.Unkeyed<IContract>();

        var dependency = new ComponentDependencyDescriptor(
            reference,
            ComponentDependencyCardinality.OptionalSingular);

        dependency.Reference.ShouldBe(reference);
        dependency.Cardinality.ShouldBe(ComponentDependencyCardinality.OptionalSingular);
        dependency.FactoryBoundary.ShouldBeNull();
    }

    [Fact]
    public void FactoryBoundaryConstructor_WhenDisposalContractIsUnsupported_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new ComponentFactoryBoundary(
            ComponentContractReference.Unkeyed<IContract>(), ComponentContractReference.Unkeyed<IContract>(), typeof(string)));

        exception.ParamName.ShouldBe("disposalContractType");
    }

    [Fact]
    public void FactoryBoundaryConstructor_WhenOwnerOrRootIsNull_ThrowsExactArgumentNullException()
    {
        var ownerException = Should.Throw<ArgumentNullException>(() => new ComponentFactoryBoundary(
            null!, ComponentContractReference.Unkeyed<IContract>(), typeof(IDisposable)));
        var rootException = Should.Throw<ArgumentNullException>(() => new ComponentFactoryBoundary(
            ComponentContractReference.Unkeyed<IContract>(), null!, typeof(IDisposable)));

        ownerException.GetType().ShouldBe(typeof(ArgumentNullException));
        rootException.GetType().ShouldBe(typeof(ArgumentNullException));
        ownerException.ParamName.ShouldBe("owner");
        rootException.ParamName.ShouldBe("operationRoot");
    }

    private interface IContract;

    private interface IGenericContract<T>;

    private sealed class Implementation: IContract;

    private abstract class AbstractImplementation: IContract;

    private sealed class UnrelatedImplementation;
}
