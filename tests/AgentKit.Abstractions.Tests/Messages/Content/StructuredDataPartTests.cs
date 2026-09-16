// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies StructuredDataPart behavior and contracts.</summary>
public sealed class StructuredDataPartTests
{
    [Fact]
    public void StructuredDataPart_WhenSchemaIsNull_Succeeds()
    {
        var part = new StructuredDataPart(default, null, ExtensionData.Empty);
        part.Schema.ShouldBeNull();
    }

    [Fact]
    public void StructuredDataPart_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new StructuredDataPart(default, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void StructuredDataPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new StructuredDataPart(default, null, ExtensionData.Empty);
        var second = new StructuredDataPart(default, null, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new StructuredDataPart(default, null, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
