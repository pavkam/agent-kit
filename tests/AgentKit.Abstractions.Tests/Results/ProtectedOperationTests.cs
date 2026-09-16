// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class ProtectedOperationTests
{
    [Theory]
    [InlineData("audience")]
    [InlineData("kind")]
    [InlineData("effect")]
    public void Constructor_WhenProtectedOperationIdentityIsInvalid_RejectsExactArgument(string parameter)
    {
        var basis = RunResultTestData.ProtectedOperation();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ProtectedOperation(
            parameter == "audience" ? default : basis.Audience, parameter == "kind" ? (SecurityOperationKind) (-1) : basis.Kind,
            parameter == "effect" ? (SecurityEffect) (-1) : basis.Effect, basis.Resources));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("default", "argument")]
    [InlineData("empty", "argument")]
    [InlineData("null", "null")]
    [InlineData("duplicate", "argument")]
    [InlineData("kind", "range")]
    [InlineData("identifier", "argument")]
    [InlineData("null-identifier", "null")]
    public void Constructor_WhenProtectedResourcesAreInvalid_RevalidatesRetainedValues(string invalid, string exceptionKind)
    {
        var basis = RunResultTestData.ProtectedOperation(); var resource = basis.Resources[0];
        ImmutableArray<ProtectedResource> resources = invalid switch
        {
            "default" => default,
            "empty" => [],
            "null" => [null!],
            "duplicate" => [resource, resource],
            "kind" => [resource with { Kind = (ProtectedResourceKind) (-1) }],
            "identifier" => [resource with { Identifier = " " }],
            _ => [resource with { Identifier = null! }],
        };
        var exception = Should.Throw<ArgumentException>(() => new ProtectedOperation(basis.Audience, basis.Kind, basis.Effect, resources));
        exception.ParamName.ShouldBe("resources");
        exception.GetType().ShouldBe(exceptionKind == "range" ? typeof(ArgumentOutOfRangeException) : exceptionKind == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void Equals_WhenResourcesAreReconstructed_UsesOrderedStructuralEquality()
    {
        var first = RunResultTestData.ProtectedOperation();
        var second = RunResultTestData.ProtectedOperation();
        first.ShouldBe(second); first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = RunResultTestData.ProtectedOperation();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
