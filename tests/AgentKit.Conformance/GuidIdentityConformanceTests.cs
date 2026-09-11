// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Verifies the shared value contract using typed fixture operations.</summary>
/// <typeparam name="T">The concrete subject constructed by its owning fixture.</typeparam>
public abstract class GuidIdentityConformanceTests<T> where T : struct
{
    /// <summary>Constructs the concrete subject through its public typed constructor.</summary>
    /// <param name="value">The value to pass without transformation.</param>
    /// <returns>The constructed subject.</returns>
    protected abstract T Create(Guid value);

    /// <summary>Reads the concrete subject through its public typed accessor.</summary>
    /// <param name="subject">The constructed subject.</param>
    /// <returns>The retained value.</returns>
    protected abstract Guid GetValue(T subject);

    /// <summary>The public constructor enforces its documented input domain.</summary>
    [Fact]
    public void Constructor_WhenGuidIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(Guid.Empty));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>The public constructor enforces its documented input domain.</summary>
    [Fact]
    public void Constructor_WhenGuidIsNonEmpty_RoundTripsThroughValue()
    {
        var value = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        GetValue(Create(value)).ShouldBe(value);
    }

    /// <summary>Diagnostic formatting preserves the canonical retained value.</summary>
    [Fact]
    public void ToString_WhenGuidBacked_ReturnsCanonicalGuidFormat()
    {
        var value = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        Create(value).ToString().ShouldBe(value.ToString("D"));
    }

    /// <summary>Equal constructed values have matching equality and hash codes.</summary>
    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreStructurallyEqual()
    {
        var first = Create(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
        var second = Create(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Distinct inputs remain distinguishable after construction.</summary>
    [Fact]
    public void Equality_WhenValuesDiffer_InstancesAreNotEqual() => Create(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef")).ShouldNotBe(Create(Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210")));
}
