// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies DurabilityProfileKey behavior and contracts.</summary>
public sealed class DurabilityProfileKeyTests: Conformance.StringIdentityConformanceTests<DurabilityProfileKey>
{
    /// <inheritdoc/>
    protected override DurabilityProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(DurabilityProfileKey subject) => subject.Value;
    [Fact]
    public void TextKey_Constructor_WhenValueIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurabilityProfileKey(null!));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurabilityProfileKey(string.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurabilityProfileKey("   "));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_ToString_ReturnsCanonicalText() => new DurabilityProfileKey("canonical").ToString().ShouldBe("canonical");
}
