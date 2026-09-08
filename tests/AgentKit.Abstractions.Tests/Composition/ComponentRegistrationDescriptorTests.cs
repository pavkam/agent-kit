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
            ComponentContractReference.Unkeyed<IContract>(), (ComponentDependencyCardinality)99));

        exception.ParamName.ShouldBe("cardinality");
    }

    [Fact]
    public void FactoryBoundaryConstructor_WhenDisposalContractIsUnsupported_ThrowsWithParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new ComponentFactoryBoundary(
            ComponentContractReference.Unkeyed<IContract>(), ComponentContractReference.Unkeyed<IContract>(), typeof(string)));

        exception.ParamName.ShouldBe("disposalContractType");
    }

    private interface IContract;

    private interface IGenericContract<T>;

    private sealed class Implementation : IContract;

    private abstract class AbstractImplementation : IContract;

    private sealed class UnrelatedImplementation;
}
