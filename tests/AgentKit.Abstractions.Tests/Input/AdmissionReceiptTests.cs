// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies AdmissionReceipt behavior and contracts.</summary>
public sealed class AdmissionReceiptTests
{
    [Fact]
    public void AdmissionReceipt_WhenSequenceIsZero_ThrowsArgumentOutOfRangeExceptionBeforeConstruction()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new AdmissionReceipt(Admission(1), Input(1), Agent(), Session(), Lane(), new SessionSequence(0), false));
        exception.ParamName.ShouldBe("admittedSequence");
    }

    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        yield return new object?[]
        {
            () => new AdmissionReceipt(default, Input(1), Agent(), Session(), Lane(), new SessionSequence(1), false),
            typeof(ArgumentOutOfRangeException),
            "admissionId"
        };
        yield return new object?[]
        {
            () => new AdmissionReceipt(Admission(1), default, Agent(), Session(), Lane(), new SessionSequence(1), false),
            typeof(ArgumentOutOfRangeException),
            "inputId"
        };
        yield return new object?[]
        {
            () => new AdmissionReceipt(Admission(1), Input(1), default, Session(), Lane(), new SessionSequence(1), false),
            typeof(ArgumentOutOfRangeException),
            "agentId"
        };
        yield return new object?[]
        {
            () => new AdmissionReceipt(Admission(1), Input(1), Agent(), default, Lane(), new SessionSequence(1), false),
            typeof(ArgumentOutOfRangeException),
            "sessionId"
        };
        yield return new object?[]
        {
            () => new AdmissionReceipt(Admission(1), Input(1), Agent(), Session(), default, new SessionSequence(1), false),
            typeof(ArgumentOutOfRangeException),
            "executionLaneId"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<AdmissionReceipt> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }
}
