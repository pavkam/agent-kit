// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies DurableOperationVersion behavior and contracts.</summary>
public sealed class DurableOperationVersionTests: Conformance.StringIdentityConformanceTests<DurableOperationVersion>
{
    /// <inheritdoc/>
    protected override DurableOperationVersion Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(DurableOperationVersion subject) => subject.Value;
    [Fact]
    public void TextKey_Constructor_WhenValueIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableOperationVersion(null!));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableOperationVersion(string.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableOperationVersion("   "));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_ToString_ReturnsCanonicalText() => new DurableOperationVersion("canonical").ToString().ShouldBe("canonical");
}
