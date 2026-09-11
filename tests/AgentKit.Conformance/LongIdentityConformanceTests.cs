// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Verifies the shared value contract using typed fixture operations.</summary>
/// <typeparam name="T">The concrete subject constructed by its owning fixture.</typeparam>
public abstract class LongIdentityConformanceTests<T> where T : struct
{
    /// <summary>Constructs the concrete subject through its public typed constructor.</summary>
    /// <param name="value">The value to pass without transformation.</param>
    /// <returns>The constructed subject.</returns>
    protected abstract T Create(long value);

    /// <summary>Reads the concrete subject through its public typed accessor.</summary>
    /// <param name="subject">The constructed subject.</param>
    /// <returns>The retained value.</returns>
    protected abstract long GetValue(T subject);

    /// <summary>States whether the concrete contract rejects zero.</summary>
    protected abstract bool RequiresPositiveValue { get; }

    /// <summary>The public constructor enforces its documented input domain.</summary>
    [Fact]
    public void Constructor_WhenLongIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(-1));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>The public constructor enforces its documented input domain.</summary>
    [Fact]
    public void Constructor_WhenLongIsWithinDeclaredDomain_RoundTripsOrRejectsZero()
    {
        if (RequiresPositiveValue)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => Create(0)).ParamName.ShouldBe("value");
        }
        else
        {
            GetValue(Create(0)).ShouldBe(0);
        }
        GetValue(Create(42)).ShouldBe(42);
    }

    /// <summary>Diagnostic formatting preserves the canonical retained value.</summary>
    [Fact]
    public void ToString_WhenLongBacked_ReturnsInvariantCultureText() => Create(42).ToString().ShouldBe(42L.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Equal constructed values have matching equality and hash codes.</summary>
    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreStructurallyEqual()
    {
        var first = Create(8L);
        var second = Create(8L);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Distinct inputs remain distinguishable after construction.</summary>
    [Fact]
    public void Equality_WhenValuesDiffer_InstancesAreNotEqual() => Create(8L).ShouldNotBe(Create(9L));
}
