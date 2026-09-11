// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Verifies the shared value contract using typed fixture operations.</summary>
/// <typeparam name="T">The concrete subject constructed by its owning fixture.</typeparam>
public abstract class SingleMessageLeafConformanceTests<T> where T : class
{
    /// <summary>Constructs the concrete subject through its public typed constructor.</summary>
    /// <param name="message">The value to pass without transformation.</param>
    /// <returns>The constructed subject.</returns>
    protected abstract T Create(string message);

    /// <summary>Reads the concrete subject through its public typed accessor.</summary>
    /// <param name="subject">The constructed subject.</param>
    /// <returns>The retained value.</returns>
    protected abstract string GetValue(T subject);

    /// <summary>The typed public constructor enforces and preserves its message contract.</summary>
    [Fact]
    public void Constructor_WhenMessageIsNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => Create(null!));

    /// <summary>The typed public constructor enforces and preserves its message contract.</summary>
    [Fact]
    public void Constructor_WhenMessageIsEmptyOrWhitespace_ThrowsArgumentException()
    {
        foreach (var invalid in new[] { string.Empty, "   " })
        {
            Should.Throw<ArgumentException>(() => Create(invalid)).GetType().ShouldBe(typeof(ArgumentException));
        }
    }

    /// <summary>The typed public constructor enforces and preserves its message contract.</summary>
    [Fact]
    public void Constructor_WhenMessageIsValid_RoundTripsThroughStringProperty() => GetValue(Create("sample-value")).ShouldBe("sample-value");

    /// <summary>Equal message values have matching equality and hash codes.</summary>
    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreStructurallyEqual()
    {
        var first = Create("sample-value-1");
        var second = Create("sample-value-1");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Different messages remain distinguishable.</summary>
    [Fact]
    public void Equality_WhenValuesDiffer_InstancesAreNotEqual() => Create("sample-value-1").ShouldNotBe(Create("sample-value-2"));
}
