// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies JsonSchemaReference behavior and contracts.</summary>
public sealed class JsonSchemaReferenceTests
{
    [Fact]
    public void JsonSchemaReference_Constructor_WhenNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new JsonSchemaReference(" ", new SchemaVersion("1")));
    [Fact]
    public void JsonSchemaReference_Constructor_WhenValid_RoundTripsProperties()
    {
        var reference = new JsonSchemaReference("schema", new SchemaVersion("1"));
        reference.Name.ShouldBe("schema");
        reference.Version.ShouldBe(new SchemaVersion("1"));
    }

    [Fact]
    public void JsonSchemaReference_Equality_WhenSameValues_InstancesAreEqual() => new JsonSchemaReference("schema", new SchemaVersion("1")).ShouldBe(new JsonSchemaReference("schema", new SchemaVersion("1")));
    [Fact]
    public void JsonSchemaReference_WhenNameIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new JsonSchemaReference("   ", new SchemaVersion("1")));
        exception.ParamName.ShouldBe("name");
    }
}
