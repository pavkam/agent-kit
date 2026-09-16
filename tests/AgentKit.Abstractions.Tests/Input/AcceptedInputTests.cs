// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies AcceptedInput behavior and contracts.</summary>
public sealed class AcceptedInputTests
{
    [Fact]
    public void InputPromotionPlanAndResults_WhenRequiredEvidenceIsNull_ThrowArgumentNullExceptionWithParamName()
    {
        var acceptedException = ShouldThrowExactly<ArgumentNullException>(() => new AcceptedInput(null!));
        acceptedException.ParamName.ShouldBe("receipt");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsReceipt()
    {
        var receipt = new AdmissionReceipt(new AdmissionId(Guid.Parse("00000000-0000-0000-0000-000000000001")), new InputId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new ExecutionLaneId(Guid.Parse("40000000-0000-0000-0000-000000000001")), new SessionSequence(1), false);
        var accepted = new AcceptedInput(receipt);
        accepted.Receipt.ShouldBeSameAs(receipt);
        InputAdmissionResult result = accepted;
        _ = result.ShouldBeOfType<AcceptedInput>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var receipt = new AdmissionReceipt(new AdmissionId(Guid.Parse("00000000-0000-0000-0000-000000000001")), new InputId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new ExecutionLaneId(Guid.Parse("40000000-0000-0000-0000-000000000001")), new SessionSequence(1), false);
        var original = new AcceptedInput(receipt);
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
