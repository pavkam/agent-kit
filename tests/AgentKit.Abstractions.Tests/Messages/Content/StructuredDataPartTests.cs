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
    public void StructuredDataPart_Equality_WhenSameValues_InstancesAreEqual() => new StructuredDataPart(default, null, ExtensionData.Empty).ShouldBe(new StructuredDataPart(default, null, ExtensionData.Empty));
}
