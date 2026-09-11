// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies DurableLeaseManagerKey behavior and contracts.</summary>
public sealed class DurableLeaseManagerKeyTests: Conformance.StringIdentityConformanceTests<DurableLeaseManagerKey>
{
    /// <inheritdoc/>
    protected override DurableLeaseManagerKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(DurableLeaseManagerKey subject) => subject.Value;
    [Fact]
    public void TextKey_Constructor_WhenValueIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableLeaseManagerKey(null!));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableLeaseManagerKey(string.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableLeaseManagerKey("   "));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_ToString_ReturnsCanonicalText() => new DurableLeaseManagerKey("canonical").ToString().ShouldBe("canonical");
}
