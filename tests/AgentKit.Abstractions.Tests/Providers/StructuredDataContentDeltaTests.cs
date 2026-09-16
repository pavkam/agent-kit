// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies StructuredDataContentDelta behavior and contracts.</summary>
public sealed class StructuredDataContentDeltaTests
{
    [Fact]
    public void StructuredDataContentDelta_Constructor_WhenFragmentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new StructuredDataContentDelta(null!));
        exception.ParamName.ShouldBe("jsonFragment");
    }

    [Fact]
    public void StructuredDataContentDelta_Constructor_WhenValid_RoundTripsFragment()
    {
        var delta = new StructuredDataContentDelta( /*lang=json,strict*/"""{"a":1}""");
        delta.JsonFragment.ShouldBe( /*lang=json,strict*/"""{"a":1}""");
    }

    [Fact]
    public void StructuredDataContentDelta_Equality_WhenSameFragment_InstancesAreEqual() => new StructuredDataContentDelta("{}").ShouldBe(new StructuredDataContentDelta("{}"));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new StructuredDataContentDelta("{}");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
