// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies the persistent grant-store identity rejects an unusable value and exposes stable safe text.</summary>
public sealed class JsonSecurityGrantStoreInstanceIdTests
{
    /// <summary>Verifies the empty identity is rejected with the exact parameter name rather than becoming a wildcard.</summary>
    /// <remarks>An empty identity would match a zero-filled or newly created manifest, defeating the replaced-root check.</remarks>
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRange()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            static () => new JsonSecurityGrantStoreInstanceId(Guid.Empty));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies the default struct value carries the empty identity that construction refuses.</summary>
    /// <remarks>The target constructor relies on this to reject a caller that never supplied bootstrap identity at all.</remarks>
    [Fact]
    public void Value_WhenDefault_IsEmptyGuid()
    {
        JsonSecurityGrantStoreInstanceId instanceId = default;

        instanceId.Value.ShouldBe(Guid.Empty);
        instanceId.ShouldBe(default);
    }

    /// <summary>Verifies a nonempty identity retains its exact value and compares by value.</summary>
    [Fact]
    public void Equals_WhenValuesMatch_ComparesByValue()
    {
        var value = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");

        var first = new JsonSecurityGrantStoreInstanceId(value);
        var second = new JsonSecurityGrantStoreInstanceId(value);

        first.Value.ShouldBe(value);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(new JsonSecurityGrantStoreInstanceId(Guid.Parse("11111111-1111-1111-1111-111111111111")));
    }

    /// <summary>Verifies the safe diagnostic text is the canonical hyphenated lowercase form.</summary>
    [Fact]
    public void ToString_WhenIdentityIsSet_ReturnsCanonicalHyphenatedText()
    {
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.Parse("1A2B3C4D-5E6F-4A7B-8C9D-0E1F2A3B4C5D"));

        var text = instanceId.ToString();

        text.ShouldBe("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");
    }
}
