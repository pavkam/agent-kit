// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Extensibility;

using AgentKit;

public sealed class ExtensionValueTests
{
    [Fact]
    public void Constructor_WhenCanonicalJsonIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ExtensionValue(default));

        exception.ParamName.ShouldBe("canonicalJson");
    }

    [Fact]
    public void Constructor_WhenCanonicalJsonIsEmpty_Succeeds()
    {
        var value = new ExtensionValue([]);

        value.CanonicalJson.ShouldBe([]);
    }

    [Fact]
    public void Constructor_WhenCanonicalJsonIsPopulated_RoundTrips()
    {
        ImmutableArray<byte> bytes = [1, 2, 3];

        var value = new ExtensionValue(bytes);

        value.CanonicalJson.ShouldBe(bytes);
    }

    [Fact]
    public void Equality_WhenBytesMatch_InstancesAreEqual()
    {
        ImmutableArray<byte> bytes = [1, 2, 3];

        var first = new ExtensionValue(bytes);
        var second = new ExtensionValue([1, 2, 3]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenBytesDiffer_InstancesAreNotEqual()
    {
        var first = new ExtensionValue([1, 2, 3]);
        var second = new ExtensionValue([1, 2, 4]);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equality_WhenLengthsDiffer_InstancesAreNotEqual()
    {
        var first = new ExtensionValue([1, 2, 3]);
        var second = new ExtensionValue([1, 2]);

        first.ShouldNotBe(second);
    }
}
