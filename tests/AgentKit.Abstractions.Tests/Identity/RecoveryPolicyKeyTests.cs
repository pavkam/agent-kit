// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies RecoveryPolicyKey behavior and contracts.</summary>
public sealed class RecoveryPolicyKeyTests: Conformance.StringIdentityConformanceTests<RecoveryPolicyKey>
{
    /// <inheritdoc/>
    protected override RecoveryPolicyKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(RecoveryPolicyKey subject) => subject.Value;
    [Fact]
    public void TextKey_Constructor_WhenValueIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryPolicyKey(null!));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryPolicyKey(string.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryPolicyKey("   "));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_ToString_ReturnsCanonicalText() => new RecoveryPolicyKey("canonical").ToString().ShouldBe("canonical");
}
