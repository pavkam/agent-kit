// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies AgentInput behavior and contracts.</summary>
public sealed class AgentInputTests
{
    [Fact]
    public void AgentInput_WhenDeliveryIsUndefined_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new AgentInput(Input(1), (InputDelivery) 42, [Part()], ExtensionData.Empty));
        exception.ParamName.ShouldBe("delivery");
    }

    [Fact]
    public void AgentInput_WhenPartsAreDefaultOrEmpty_ThrowsArgumentExceptionWithParamName()
    {
        ImmutableArray<ContentPart> uninitialized = default;
        var defaultException = ShouldThrowExactly<ArgumentException>(() => new AgentInput(Input(1), InputDelivery.Steer, uninitialized, ExtensionData.Empty));
        var emptyException = ShouldThrowExactly<ArgumentException>(() => new AgentInput(Input(1), InputDelivery.Steer, [], ExtensionData.Empty));
        defaultException.ParamName.ShouldBe("parts");
        emptyException.ParamName.ShouldBe("parts");
    }

    private static TextPart Part() => new("input", TextSemantics.Plain, ExtensionData.Empty);
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        ExtensionData? nullExtensions = null;
        ImmutableArray<ContentPart> nullParts = [null!];
        yield return new object?[]
        {
            () => new AgentInput(default, InputDelivery.Steer, [Part()], ExtensionData.Empty),
            typeof(ArgumentOutOfRangeException),
            "id"
        };
        yield return new object?[]
        {
            () => new AgentInput(Input(1), InputDelivery.Steer, nullParts, ExtensionData.Empty),
            typeof(ArgumentException),
            "parts"
        };
        yield return new object?[]
        {
            () => new AgentInput(Input(1), InputDelivery.Steer, [Part()], nullExtensions!),
            typeof(ArgumentNullException),
            "extensions"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<AgentInput> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }
}
