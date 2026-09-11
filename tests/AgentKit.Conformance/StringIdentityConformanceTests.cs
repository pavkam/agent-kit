// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Verifies the shared value contract using typed fixture operations.</summary>
/// <typeparam name="T">The concrete subject constructed by its owning fixture.</typeparam>
public abstract class StringIdentityConformanceTests<T> where T : struct
{
    /// <summary>Constructs the concrete subject through its public typed constructor.</summary>
    /// <param name="value">The value to pass without transformation.</param>
    /// <returns>The constructed subject.</returns>
    protected abstract T Create(string value);

    /// <summary>Reads the concrete subject through its public typed accessor.</summary>
    /// <param name="subject">The constructed subject.</param>
    /// <returns>The retained value.</returns>
    protected abstract string? GetValue(T subject);

    /// <summary>The public constructor enforces its documented input domain.</summary>
    [Fact]
    public void Constructor_WhenStringIsNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => Create(null!));

    /// <summary>The public constructor enforces its documented input domain.</summary>
    [Fact]
    public void Constructor_WhenStringIsEmptyOrWhitespace_ThrowsArgumentException()
    {
        foreach (var invalid in new[] { string.Empty, "   " })
        {
            Should.Throw<ArgumentException>(() => Create(invalid)).GetType().ShouldBe(typeof(ArgumentException));
        }
    }

    /// <summary>The public constructor enforces its documented input domain.</summary>
    [Fact]
    public void Constructor_WhenStringIsValid_RoundTripsThroughValue() => GetValue(Create("sample-value")).ShouldBe("sample-value");

    /// <summary>Diagnostic formatting preserves the canonical retained value.</summary>
    [Fact]
    public void ToString_WhenStringBacked_ReturnsUnderlyingText() => Create("sample-value").ToString().ShouldBe("sample-value");

    /// <summary>Equal constructed values have matching equality and hash codes.</summary>
    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreStructurallyEqual()
    {
        var first = Create("sample-value-1");
        var second = Create("sample-value-1");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Distinct inputs remain distinguishable after construction.</summary>
    [Fact]
    public void Equality_WhenValuesDiffer_InstancesAreNotEqual() => Create("sample-value-1").ShouldNotBe(Create("sample-value-2"));
}
