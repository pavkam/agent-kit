// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies the persistent approval-store identity rejects an unusable value and exposes stable safe text.</summary>
public sealed class JsonApprovalStoreInstanceIdTests
{
    /// <summary>Verifies the empty identity is rejected with the exact parameter name rather than becoming a wildcard.</summary>
    /// <remarks>Approvals are human authority evidence, so an identity that matched any root would let one deployment resolve another's pending requests.</remarks>
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRange()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            static () => new JsonApprovalStoreInstanceId(Guid.Empty));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies the default struct value carries the empty identity that construction refuses.</summary>
    [Fact]
    public void Value_WhenDefault_IsEmptyGuid()
    {
        JsonApprovalStoreInstanceId instanceId = default;

        instanceId.Value.ShouldBe(Guid.Empty);
        instanceId.ShouldBe(default);
    }

    /// <summary>Verifies a nonempty identity retains its exact value and compares by value.</summary>
    [Fact]
    public void Equals_WhenValuesMatch_ComparesByValue()
    {
        var value = Guid.Parse("9f8e7d6c-5b4a-4938-8271-605f4e3d2c1b");

        var first = new JsonApprovalStoreInstanceId(value);
        var second = new JsonApprovalStoreInstanceId(value);

        first.Value.ShouldBe(value);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(new JsonApprovalStoreInstanceId(Guid.Parse("22222222-2222-2222-2222-222222222222")));
    }

    /// <summary>Verifies the safe diagnostic text is the canonical hyphenated lowercase form.</summary>
    [Fact]
    public void ToString_WhenIdentityIsSet_ReturnsCanonicalHyphenatedText()
    {
        var instanceId = new JsonApprovalStoreInstanceId(Guid.Parse("9F8E7D6C-5B4A-4938-8271-605F4E3D2C1B"));

        var text = instanceId.ToString();

        text.ShouldBe("9f8e7d6c-5b4a-4938-8271-605f4e3d2c1b");
    }
}
