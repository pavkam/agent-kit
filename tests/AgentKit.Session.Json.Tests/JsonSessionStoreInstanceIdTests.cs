// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the validated nondefault JSON session-store instance identity.</summary>
public sealed class JsonSessionStoreInstanceIdTests
{
    /// <summary>Verifies a nonempty value is retained unchanged.</summary>
    [Fact]
    public void Constructor_WhenValueIsNonempty_RetainsValue()
    {
        var value = Guid.NewGuid();

        var identity = new JsonSessionStoreInstanceId(value);

        identity.Value.ShouldBe(value);
    }

    /// <summary>Verifies an empty GUID is rejected.</summary>
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreInstanceId(Guid.Empty))
            .ParamName.ShouldBe("value");

    /// <summary>Verifies the canonical hyphenated textual representation is used for safe diagnostics.</summary>
    [Fact]
    public void ToString_WhenCalled_ReturnsHyphenatedIdentityText()
    {
        var value = Guid.NewGuid();

        var identity = new JsonSessionStoreInstanceId(value);

        identity.ToString().ShouldBe(value.ToString("D"));
    }

    /// <summary>Verifies two identities built from the same value compare equal.</summary>
    [Fact]
    public void Equals_WhenValuesMatch_IsEqual()
    {
        var value = Guid.NewGuid();

        new JsonSessionStoreInstanceId(value).ShouldBe(new JsonSessionStoreInstanceId(value));
    }
}
