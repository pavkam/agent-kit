// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies QueueCapacityExceeded behavior and contracts.</summary>
public sealed class QueueCapacityExceededTests
{
    [Fact]
    public void InputCapacityAndRejectionEvidence_WhenArgumentsAreInvalid_ThrowsExactExpectedException()
    {
        var retryException = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new QueueCapacityExceeded(new InputCapacityLimit(1, 0), TimeSpan.FromTicks(-1)));
        retryException.ParamName.ShouldBe("retryAfter");
    }

    [Fact]
    public void Constructor_WhenLimitIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new QueueCapacityExceeded(null!, null)).ParamName.ShouldBe("limit");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var limit = new InputCapacityLimit(1, 0);
        var exceeded = new QueueCapacityExceeded(limit, TimeSpan.FromSeconds(1));
        exceeded.Limit.ShouldBeSameAs(limit);
        exceeded.RetryAfter.ShouldBe(TimeSpan.FromSeconds(1));
        InputAdmissionResult result = exceeded;
        _ = result.ShouldBeOfType<QueueCapacityExceeded>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new QueueCapacityExceeded(new InputCapacityLimit(1, 0), TimeSpan.FromSeconds(1));
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
