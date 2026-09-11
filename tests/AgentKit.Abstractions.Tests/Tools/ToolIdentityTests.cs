// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;



/// <summary>Verifies ToolIdentity behavior and contracts.</summary>
public sealed class ToolIdentityTests
{
    [Fact]
    public void ToolIdentity_Constructor_WhenIdDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolIdentity(default, new ToolVersion("1")));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void ToolIdentity_Constructor_WhenVersionDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolIdentity(new ToolId("read"), default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ToolIdentity_Constructor_WhenValuesValid_PreservesExactPair()
    {
        var identity = new ToolIdentity(new ToolId("read"), new ToolVersion("2"));
        identity.Id.ShouldBe(new ToolId("read"));
        identity.Version.ShouldBe(new ToolVersion("2"));
    }

    [Fact]
    public void ValueDefaults_WhenComparedAndFormatted_RemainUninitializedValues() => default(ToolIdentity).ShouldBe(default);
}
