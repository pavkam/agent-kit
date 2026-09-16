// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ToolReference behavior and contracts.</summary>
public sealed class ToolReferenceTests
{
    [Fact]
    public void ToolReference_Constructor_WhenProviderAliasIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolReference(default, null, null));
        exception.ParamName.ShouldBe("providerAlias");
    }

    [Fact]
    public void ToolReference_Constructor_WhenIdPresentWithoutVersion_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolReference(new ToolAlias("read"), new ToolId("read"), null));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ToolReference_Constructor_WhenVersionPresentWithoutId_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolReference(new ToolAlias("read"), null, new ToolVersion("1")));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ToolReference_Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolReference(new ToolAlias("read"), default(ToolId), new ToolVersion("1")));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void ToolReference_Constructor_WhenVersionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolReference(new ToolAlias("read"), new ToolId("read"), default(ToolVersion)));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ToolReference_Constructor_WhenUnresolved_ConstructsWithNullIdAndVersion()
    {
        var reference = new ToolReference(new ToolAlias("read"), null, null);

        reference.ProviderAlias.ShouldBe(new ToolAlias("read"));
        reference.Id.ShouldBeNull();
        reference.Version.ShouldBeNull();
        reference.IsResolved.ShouldBeFalse();
    }

    [Fact]
    public void ToolReference_Constructor_WhenResolved_ConstructsWithIdAndVersion()
    {
        var reference = new ToolReference(new ToolAlias("read"), new ToolId("tool.read"), new ToolVersion("1"));

        reference.ProviderAlias.ShouldBe(new ToolAlias("read"));
        reference.Id.ShouldBe(new ToolId("tool.read"));
        reference.Version.ShouldBe(new ToolVersion("1"));
        reference.IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void ToolReference_Equality_WhenSameValues_InstancesAreEqual() =>
        new ToolReference(new ToolAlias("t"), new ToolId("t"), new ToolVersion("1")).ShouldBe(
            new ToolReference(new ToolAlias("t"), new ToolId("t"), new ToolVersion("1")));

    [Fact]
    public void ToolReference_Equality_WhenOneUnresolved_InstancesAreNotEqual() =>
        new ToolReference(new ToolAlias("t"), null, null).ShouldNotBe(
            new ToolReference(new ToolAlias("t"), new ToolId("t"), new ToolVersion("1")));

    [Fact]
    public void With_WhenProviderAliasIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var reference = new ToolReference(new ToolAlias("t"), null, null);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => reference with { ProviderAlias = default });

        exception.ParamName.ShouldBe("value");
    }
}
