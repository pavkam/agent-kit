// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolError behavior and contracts.</summary>
public sealed class ToolErrorTests
{
    [Fact]
    public void ToolError_Constructor_WhenRetryDelayNegative_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolError(ToolErrorKind.Tool, "safe", null, TimeSpan.FromTicks(-1), ExtensionData.Empty));
        exception.ParamName.ShouldBe("retryAfter");
    }

    [Fact]
    public void ToolError_Constructor_WhenExternalCodeBlank_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolError(ToolErrorKind.Tool, "safe", " ", null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("externalCode");
    }

    [Fact]
    public void ToolError_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolError(ToolErrorKind.Tool, "safe", null, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ToolError_Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var error = new ToolError(ToolErrorKind.Tool, "safe", "code", TimeSpan.FromSeconds(1), ExtensionData.Empty);
        error.Kind.ShouldBe(ToolErrorKind.Tool);
        error.SafeMessage.ShouldBe("safe");
        error.ExternalCode.ShouldBe("code");
        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(1));
        error.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolError(ToolErrorKind.Tool, "safe", "code", TimeSpan.FromSeconds(1), ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
