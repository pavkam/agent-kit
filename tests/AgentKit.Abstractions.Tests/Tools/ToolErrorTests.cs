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
}
