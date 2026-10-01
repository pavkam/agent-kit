// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

using AgentKit;

public sealed class ArgumentOutOfRangeExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfUndefined_WhenValueIsDefined_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfUndefined(FileWriteDisposition.Append));

    [Fact]
    public void ThrowIfUndefined_WhenValueIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var value = (FileWriteDisposition) int.MaxValue;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfUndefined(value));

        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(value);
    }

    [Fact]
    public void ThrowIfUndefined_WhenParamNameSuppliedExplicitly_UsesSuppliedName()
    {
        var value = (FileWriteDisposition) int.MaxValue;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfUndefined(value, "customParam"));

        exception.ParamName.ShouldBe("customParam");
    }

    [Theory]
    [InlineData((int) DurableOperationState.OutcomeReady)]
    [InlineData((int) DurableOperationState.Completed)]
    [InlineData((int) DurableOperationState.Faulted)]
    public void ThrowIfNotTerminalDurableOperationState_WhenValueRetainsACompleteResult_DoesNotThrow(int rawValue) =>
        Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfNotTerminalDurableOperationState((DurableOperationState) rawValue));

    [Theory]
    [InlineData((int) DurableOperationState.Accepted)]
    [InlineData((int) DurableOperationState.EffectPending)]
    [InlineData((int) DurableOperationState.Waiting)]
    [InlineData(int.MaxValue)]
    public void ThrowIfNotTerminalDurableOperationState_WhenValueDoesNotRetainACompleteResult_ThrowsExactInferredParameter(int rawValue)
    {
        var value = (DurableOperationState) rawValue;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfNotTerminalDurableOperationState(value));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(value);
    }

    [Fact]
    public void ThrowIfNotTerminalDurableOperationState_WhenParameterNameSuppliedExplicitly_UsesSuppliedName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentOutOfRangeException.ThrowIfNotTerminalDurableOperationState(
                DurableOperationState.EffectPending,
                "state"));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("state");
    }

    [Fact]
    public void ThrowIfUndefined_WhenUsedByFileSearchPattern_CoversProductionCallSite()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new FileSearchPattern("needle", (FileSearchPatternKind) int.MaxValue));

        exception.ParamName.ShouldBe("kind");
    }
}
