// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit;
using Microsoft.Extensions.DependencyInjection;

public sealed class ComponentDependencyGraphValidatorTests
{
    [Fact]
    public void Validate_WhenExactKeysAndLeafAreValid_ReturnsNoDiagnostics()
    {
        var alpha = new ComponentContractReference(typeof(ILeaf), "alpha");
        var beta = new ComponentContractReference(typeof(ILeaf), "beta");
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(alpha, typeof(AlphaLeaf), ServiceLifetime.Singleton),
            Registration(beta, typeof(BetaLeaf), ServiceLifetime.Singleton),
            Registration(Reference<IRoot>(), typeof(Root), ServiceLifetime.Singleton, Dependency(beta)),
        ]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenRequiredDependencyIsMissingOrAmbiguous_ReportsBothProblems()
    {
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(Reference<IRoot>(), typeof(Root), ServiceLifetime.Singleton, Dependency(Reference<ILeaf>())),
            Registration(Reference<ICollectionRoot>(), typeof(CollectionRoot), ServiceLifetime.Singleton, Dependency(Reference<ISecondLeaf>())),
            Registration(Reference<ISecondLeaf>(), typeof(SecondLeaf), ServiceLifetime.Transient),
            Registration(Reference<ISecondLeaf>(), typeof(AnotherSecondLeaf), ServiceLifetime.Transient),
        ]);

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-dependency.missing");
        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-dependency.ambiguous");
    }

    [Fact]
    public void Validate_WhenCollectionDependencyHasMultipleMatches_ExpandsEveryMatchWithoutAmbiguity()
    {
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(Reference<ICollectionRoot>(), typeof(CollectionRoot), ServiceLifetime.Singleton,
                Dependency(Reference<ILeaf>(), ComponentDependencyCardinality.AdditiveCollection)),
            Registration(new ComponentContractReference(typeof(ILeaf), "one"), typeof(AlphaLeaf), ServiceLifetime.Transient),
            Registration(new ComponentContractReference(typeof(ILeaf), "two"), typeof(BetaLeaf), ServiceLifetime.Transient),
        ]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenDirectAndTransitiveCyclesExist_ReportsCompleteCyclePaths()
    {
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(Reference<ILeaf>(), typeof(AlphaLeaf), ServiceLifetime.Singleton, Dependency(Reference<ILeaf>())),
            Registration(Reference<IA>(), typeof(A), ServiceLifetime.Singleton, Dependency(Reference<IB>())),
            Registration(Reference<IB>(), typeof(B), ServiceLifetime.Singleton, Dependency(Reference<IC>())),
            Registration(Reference<IC>(), typeof(C), ServiceLifetime.Singleton, Dependency(Reference<IA>())),
        ]);

        var cycles = diagnostics.Where(static diagnostic => diagnostic.Code == "agentkit.component-dependency.cycle").ToArray();
        cycles.Length.ShouldBe(2);
        cycles.ShouldContain(diagnostic => diagnostic.SafeMessage.Contains(typeof(ILeaf).FullName!, StringComparison.Ordinal));
        cycles.ShouldContain(diagnostic => diagnostic.SafeMessage.Contains(typeof(IA).FullName!, StringComparison.Ordinal)
            && diagnostic.SafeMessage.Contains(typeof(IB).FullName!, StringComparison.Ordinal)
            && diagnostic.SafeMessage.Contains(typeof(IC).FullName!, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenFactoryOperationReentersOwner_ReportsCycle()
    {
        var owner = Reference<IRoot>();
        var operation = Reference<IOperation>();
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(owner, typeof(Root), ServiceLifetime.Singleton,
                Dependency(operation, factoryBoundary: new ComponentFactoryBoundary(owner, operation, typeof(IDisposable)))),
            Registration(operation, typeof(Operation), ServiceLifetime.Scoped, Dependency(owner)),
        ]);

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-dependency.cycle");
    }

    [Fact]
    public void Validate_WhenFactoryBoundaryMetadataIsInvalid_ReportsOwnerRootAndDisposalProblems()
    {
        var owner = Reference<IRoot>();
        var operation = Reference<IOperation>();
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(owner, typeof(Root), ServiceLifetime.Singleton,
                Dependency(operation, ComponentDependencyCardinality.AdditiveCollection,
                    new ComponentFactoryBoundary(Reference<ICollectionRoot>(), Reference<ILeaf>(), typeof(IDisposable)))),
            Registration(operation, typeof(NonDisposableOperation), ServiceLifetime.Scoped),
        ]);

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-factory-boundary.owner-mismatch");
        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-factory-boundary.owner-missing");
        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-factory-boundary.root-mismatch");
    }

    [Fact]
    public void Validate_WhenFactoryRootDoesNotImplementDisposalContract_ReportsProblem()
    {
        var owner = Reference<IRoot>();
        var operation = Reference<IOperation>();
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(owner, typeof(Root), ServiceLifetime.Singleton,
                Dependency(operation, factoryBoundary: new ComponentFactoryBoundary(owner, operation, typeof(IDisposable)))),
            Registration(operation, typeof(NonDisposableOperation), ServiceLifetime.Scoped),
        ]);

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-factory-boundary.disposal-contract");
    }

    [Fact]
    public void Validate_WhenSingletonCapturesScopedDependencyThroughTransient_ReportsProblem()
    {
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(Reference<IRoot>(), typeof(Root), ServiceLifetime.Singleton, Dependency(Reference<IMiddle>())),
            Registration(Reference<IMiddle>(), typeof(Middle), ServiceLifetime.Transient, Dependency(Reference<ILeaf>())),
            Registration(Reference<ILeaf>(), typeof(AlphaLeaf), ServiceLifetime.Scoped),
        ]);

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldContain("agentkit.component-lifetime.captive-scoped");
    }

    [Fact]
    public void Validate_WhenOperationOwnedScopeIsAcyclicAndDisposable_AllowsScopedRoot()
    {
        var owner = Reference<IRoot>();
        var operation = Reference<IOperation>();
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(owner, typeof(Root), ServiceLifetime.Singleton,
                Dependency(operation, factoryBoundary: new ComponentFactoryBoundary(owner, operation, typeof(IDisposable)))),
            Registration(operation, typeof(Operation), ServiceLifetime.Scoped),
        ]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenSingletonGraphDoesNotCaptureScopedState_ReturnsNoDiagnostics()
    {
        var diagnostics = ComponentDependencyGraphValidator.Validate([
            Registration(Reference<IRoot>(), typeof(Root), ServiceLifetime.Singleton, Dependency(Reference<IMiddle>())),
            Registration(Reference<IMiddle>(), typeof(Middle), ServiceLifetime.Transient, Dependency(Reference<ILeaf>())),
            Registration(Reference<ILeaf>(), typeof(AlphaLeaf), ServiceLifetime.Singleton),
        ]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenDependencyChainIsDeep_CompletesWithoutRecursiveTraversal()
    {
        var registrations = ImmutableArray.CreateBuilder<ComponentRegistrationDescriptor>();
        Type previousArgument = typeof(Seed);
        var previous = typeof(IChain<>).MakeGenericType(previousArgument);
        registrations.Add(Registration(new ComponentContractReference(previous), typeof(Chain<>).MakeGenericType(previousArgument), ServiceLifetime.Transient));
        for (var index = 0; index < 512; index++)
        {
            var argument = typeof(Node<>).MakeGenericType(previousArgument);
            var service = typeof(IChain<>).MakeGenericType(argument);
            registrations.Add(Registration(
                new ComponentContractReference(service),
                typeof(Chain<>).MakeGenericType(argument),
                ServiceLifetime.Transient,
                Dependency(new ComponentContractReference(previous))));
            previousArgument = argument;
            previous = service;
        }

        ComponentDependencyGraphValidator.Validate(registrations.ToImmutable()).ShouldBeEmpty();
    }

    private static ComponentContractReference Reference<TContract>() where TContract : class =>
        ComponentContractReference.Unkeyed<TContract>();

    private static ComponentDependencyDescriptor Dependency(
        ComponentContractReference reference,
        ComponentDependencyCardinality cardinality = ComponentDependencyCardinality.RequiredSingular,
        ComponentFactoryBoundary? factoryBoundary = null) => new(reference, cardinality, factoryBoundary);

    private static ComponentRegistrationDescriptor Registration(
        ComponentContractReference service,
        Type implementationType,
        ServiceLifetime lifetime,
        params ComponentDependencyDescriptor[] dependencies) =>
        new(service, implementationType, lifetime, [.. dependencies]);

    private interface IRoot;
    private interface ICollectionRoot;
    private interface ILeaf;
    private interface ISecondLeaf;
    private interface IMiddle;
    private interface IOperation;
    private interface IA;
    private interface IB;
    private interface IC;
    private interface IChain<T>;

    private sealed class Root : IRoot;
    private sealed class CollectionRoot : ICollectionRoot;
    private sealed class AlphaLeaf : ILeaf;
    private sealed class BetaLeaf : ILeaf;
    private sealed class SecondLeaf : ISecondLeaf;
    private sealed class AnotherSecondLeaf : ISecondLeaf;
    private sealed class Middle : IMiddle;
    private sealed class Operation : IOperation, IDisposable { public void Dispose() { } }
    private sealed class NonDisposableOperation : IOperation;
    private sealed class A : IA;
    private sealed class B : IB;
    private sealed class C : IC;
    private sealed class Seed;
    private sealed class Node<T>;
    private sealed class Chain<T> : IChain<T>;
}
