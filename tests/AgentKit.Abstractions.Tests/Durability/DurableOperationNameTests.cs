// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableOperationName behavior and contracts.</summary>
public sealed class DurableOperationNameTests: Conformance.StringIdentityConformanceTests<DurableOperationName>
{
    [Fact]
    public void DurableOperationName_Equality_WhenSameText_InstancesAreEqual() => new DurableOperationName("tool.call").ShouldBe(new DurableOperationName("tool.call"));
    [Fact]
    public void DurableOperationName_Equality_WhenDifferentCase_InstancesAreNotEqual() => new DurableOperationName("tool.call").ShouldNotBe(new DurableOperationName("Tool.Call"));
    /// <inheritdoc/>
    protected override DurableOperationName Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(DurableOperationName subject) => subject.Value;
    [Fact]
    public void TextKey_Constructor_WhenValueIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableOperationName(null!));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableOperationName(string.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_Constructor_WhenValueIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableOperationName("   "));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void TextKey_ToString_ReturnsCanonicalText() => new DurableOperationName("canonical").ToString().ShouldBe("canonical");
}
