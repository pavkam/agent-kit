// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableOperationResult behavior and contracts.</summary>
public sealed class DurableOperationResultTests
{
    [Theory]
    [InlineData((int) DurableOperationState.OutcomeReady)]
    [InlineData((int) DurableOperationState.Completed)]
    [InlineData((int) DurableOperationState.Faulted)]
    public void DurableOperationResult_Constructor_WhenStateRetainsCompleteResult_CreatesResult(int rawState)
    {
        var state = (DurableOperationState) rawState;
        var result = Result(state);
        result.State.ShouldBe(state);
    }

    [Theory]
    [InlineData((int) DurableOperationState.Accepted)]
    [InlineData((int) DurableOperationState.EffectPending)]
    [InlineData((int) DurableOperationState.Waiting)]
    [InlineData(int.MaxValue)]
    public void DurableOperationResult_Constructor_WhenStateDoesNotRetainCompleteResult_ThrowsExactParameter(int rawState)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Result((DurableOperationState) rawState));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("state");
    }

    [Theory]
    [InlineData((int) DurableOperationState.OutcomeReady)]
    [InlineData((int) DurableOperationState.Completed)]
    [InlineData((int) DurableOperationState.Faulted)]
    public void DurableOperationResult_With_WhenStateRetainsCompleteResult_CreatesCopyWithoutChangingOriginal(int rawState)
    {
        var original = Result(DurableOperationState.Completed);
        var copy = original with
        {
            State = (DurableOperationState) rawState
        };
        copy.State.ShouldBe((DurableOperationState) rawState);
        original.State.ShouldBe(DurableOperationState.Completed);
    }

    [Theory]
    [InlineData((int) DurableOperationState.Accepted)]
    [InlineData((int) DurableOperationState.EffectPending)]
    [InlineData((int) DurableOperationState.Waiting)]
    [InlineData(int.MaxValue)]
    public void DurableOperationResult_With_WhenStateDoesNotRetainCompleteResult_ThrowsExactPropertyParameter(int rawState)
    {
        var original = DurabilityTestData.Result();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => original with { State = (DurableOperationState) rawState });
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(nameof(DurableOperationResult.State));
        original.State.ShouldBe(DurableOperationState.Completed);
    }

    [Fact]
    public void DurableOperationResult_Constructor_WhenFailureMessageOmitted_IsNull() => DurabilityTestData.Result().SafeFailureMessage.ShouldBeNull();
    [Fact]
    public void DurableOperationResult_Constructor_WhenStateIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationResult(DurabilityTestData.Address(), DurabilityTestData.Context(), (DurableOperationState) 55, SideEffectCertainty.Unknown, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("state");
    }

    private static DurableOperationResult Result(DurableOperationState state) => new(DurabilityTestData.Address(), DurabilityTestData.Context(), state, SideEffectCertainty.DefinitelyPerformed, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
}
