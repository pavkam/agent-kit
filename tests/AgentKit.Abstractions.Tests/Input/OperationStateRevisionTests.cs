// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies OperationStateRevision behavior and contracts.</summary>
public sealed class OperationStateRevisionTests: Conformance.LongIdentityConformanceTests<OperationStateRevision>
{
    [Fact]
    public void OperationStateRevision_WhenValueIsZero_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new OperationStateRevision(0));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void OperationStateRevision_WhenValueIsNegative_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new OperationStateRevision(-1));
        exception.ParamName.ShouldBe("value");
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    /// <inheritdoc/>
    protected override OperationStateRevision Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(OperationStateRevision subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
