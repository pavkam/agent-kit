// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text;

using AgentKit;

/// <summary>Verifies ToolResultOpaqueContent behavior and contracts.</summary>
public sealed class ToolResultOpaqueContentTests
{
    [Fact]
    public void ToolResultOpaqueContent_Constructor_WhenValid_RetainsOpaqueBytes()
    {
        var payload = new ExtensionValue([.. Encoding.UTF8.GetBytes( /*lang=json,strict*/"{\"future\":true}")]);
        var content = new ToolResultOpaqueContent("vendor.future", payload, ExtensionData.Empty);
        content.TypeDiscriminator.ShouldBe("vendor.future");
        content.CanonicalPayload.ShouldBe(payload);
        content.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var payload = new ExtensionValue([.. Encoding.UTF8.GetBytes( /*lang=json,strict*/"{\"future\":true}")]);
        var original = new ToolResultOpaqueContent("vendor.future", payload, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void ToolResultOpaqueContent_Constructor_WhenPayloadDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultOpaqueContent("vendor.future", default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("canonicalPayload");
    }

    [Fact]
    public void ToolResultOpaqueContent_Constructor_WhenDiscriminatorBlank_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultOpaqueContent(" ", default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("typeDiscriminator");
    }
}
