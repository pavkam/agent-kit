// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputRejection behavior and contracts.</summary>
public sealed class InputRejectionTests
{
    [Fact]
    public void InputCapacityAndRejectionEvidence_WhenArgumentsAreInvalid_ThrowsExactExpectedException()
    {
        var kindException = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new InputRejection((InputRejectionKind) 42, "safe"));
        kindException.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var rejection = new InputRejection(InputRejectionKind.Unauthorized, "safe");
        rejection.Kind.ShouldBe(InputRejectionKind.Unauthorized);
        rejection.SafeReason.ShouldBe("safe");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputRejection(InputRejectionKind.Unauthorized, "safe");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
