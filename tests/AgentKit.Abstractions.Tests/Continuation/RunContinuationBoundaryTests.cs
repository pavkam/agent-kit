// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

/// <summary>Verifies RunContinuationBoundary derived behavior and contracts.</summary>
public sealed class RunContinuationBoundaryTests
{
    private static readonly TurnId _turnId = new(Guid.Parse("d0000000-0000-0000-0000-000000000001"));
    private static readonly ModelRequestId _modelRequestId = new(Guid.Parse("d0000000-0000-0000-0000-000000000002"));
    private static readonly OperationId _operationId = new(Guid.Parse("d0000000-0000-0000-0000-000000000003"));

    [Fact]
    public void IdleContinuationBoundary_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdleContinuationBoundary();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DeferredContinuationBoundary_WhenAnyIdentityIsDefault_ThrowsExactArgumentOutOfRangeException(int invalidMember)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DeferredContinuationBoundary(
            invalidMember == 0 ? default : _turnId,
            invalidMember == 1 ? default : _modelRequestId,
            invalidMember == 2 ? default : _operationId));
        exception.ParamName.ShouldBe(invalidMember switch
        {
            0 => "turnId",
            1 => "modelRequestId",
            _ => "deferredOperationId",
        });
    }

    [Fact]
    public void DeferredContinuationBoundary_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var boundary = new DeferredContinuationBoundary(_turnId, _modelRequestId, _operationId);
        boundary.TurnId.ShouldBe(_turnId);
        boundary.ModelRequestId.ShouldBe(_modelRequestId);
        boundary.DeferredOperationId.ShouldBe(_operationId);
    }

    [Fact]
    public void DeferredContinuationBoundary_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DeferredContinuationBoundary(_turnId, _modelRequestId, _operationId);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RetryContinuationBoundary_WhenAnyIdentityIsDefault_ThrowsExactArgumentOutOfRangeException(int invalidMember)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RetryContinuationBoundary(
            invalidMember == 0 ? default : _turnId,
            invalidMember == 1 ? default : _modelRequestId));
        exception.ParamName.ShouldBe(invalidMember == 0 ? "turnId" : "modelRequestId");
    }

    [Fact]
    public void RetryContinuationBoundary_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var boundary = new RetryContinuationBoundary(_turnId, _modelRequestId);
        boundary.TurnId.ShouldBe(_turnId);
        boundary.ModelRequestId.ShouldBe(_modelRequestId);
    }

    [Fact]
    public void RetryContinuationBoundary_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RetryContinuationBoundary(_turnId, _modelRequestId);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
