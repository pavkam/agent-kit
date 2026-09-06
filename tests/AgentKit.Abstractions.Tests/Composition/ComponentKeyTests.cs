// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

public sealed class ComponentKeyTests
{
    private interface ISampleContract
    {
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsNullEmptyOrWhitespace_ThrowsArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ComponentKey<ISampleContract>(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsValid_RoundTripsThroughValue()
    {
        var key = new ComponentKey<ISampleContract>("default");

        key.Value.ShouldBe("default");
    }

    [Fact]
    public void ToString_WhenCalled_ReturnsValue()
    {
        var key = new ComponentKey<ISampleContract>("default");

        key.ToString().ShouldBe("default");
    }

    [Fact]
    public void Equality_WhenSameContractAndValue_AreEqual()
    {
        var first = new ComponentKey<ISampleContract>("default");
        var second = new ComponentKey<ISampleContract>("default");

        first.ShouldBe(second);
        (first == second).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WhenDifferentContractTypeParameter_TypesAreDistinct() =>
        // ComponentKey<T> closes over its contract type, so a key for one
        // contract can never be assigned to or compared against a key for a
        // different contract, even with identical text -- this is a
        // compile-time guarantee, demonstrated here by the two closed
        // generic types being different CLR types.
        typeof(ComponentKey<ISampleContract>).ShouldNotBe(typeof(ComponentKey<IDisposable>));
}
